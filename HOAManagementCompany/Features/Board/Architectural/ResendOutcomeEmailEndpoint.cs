using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// POST …/{applicationId}/outcome/resend-email — re-queues a failed owner outcome email (027 FR-026).
/// Allowed only when the latest outcome email failed.
/// </summary>
public class ResendOutcomeEmailEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcQueries queries,
    OutboxDispatcher dispatcher)
    : Endpoint<ArcApplicationRoute, ArcResendDto>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/architectural-applications/{applicationId}/outcome/resend-email");
        Description(x => x.WithName("ResendArchitecturalOutcomeEmail").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcApplicationRoute req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ManageArchitecturalReview, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var app = await queries.FindInCommunityAsync(req.CommunityId, req.ApplicationId, ct);
        if (app is null)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var prefix = ArcQueries.OutcomeDedupPrefix(app.Id);
        var rows = await db.OutboxMessages
            .Where(m => m.DedupKey != null && m.DedupKey.StartsWith(prefix))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        var latest = rows.FirstOrDefault();
        if (app.Status != ArcApplicationStatus.Closed || latest is null || latest.Status != OutboxStatus.Failed)
        {
            await ArcHttp.ConflictAsync(HttpContext, ArcErrorCodes.EmailNotFailed,
                "Only a failed owner email can be resent.", ct);
            return;
        }

        db.OutboxMessages.Add(new OutboxMessage
        {
            Kind = latest.Kind,
            OwnerId = latest.OwnerId,
            RecipientUserId = latest.RecipientUserId,
            DedupKey = $"{prefix}:resend:{rows.Count}",
            PayloadJson = latest.PayloadJson,
            Status = OutboxStatus.Pending
        });
        await db.SaveChangesAsync(ct);
        await dispatcher.DispatchPendingAsync(ct);

        await SendAsync(new ArcResendDto(await queries.OwnerEmailStatusAsync(app, ct)), StatusCodes.Status202Accepted, ct);
    }
}

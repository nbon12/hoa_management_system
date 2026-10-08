using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// POST …/{applicationId}/info-requests — a board member asks for more information (027 US4;
/// FR-021/FR-022). Not a vote; never changes the tally or moves the due date, which is echoed back so
/// the UI can show that questions don't pause the review period.
/// </summary>
public class InfoRequestEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ArcQueries queries)
    : Endpoint<ArcInfoRequestRequest, ArcInfoRequestCreatedDto>
{
    public const int MaxMessageLength = 2000;

    public override void Configure()
    {
        Post("/communities/{communityId}/architectural-applications/{applicationId}/info-requests");
        Description(x => x.WithName("RequestArchitecturalInfo").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcInfoRequestRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.VoteArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var message = req.Message?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            await ArcHttp.ValidationAsync(HttpContext, "message is required: say what information is needed.", ct);
            return;
        }
        if (message.Length > MaxMessageLength)
        {
            await ArcHttp.ValidationAsync(HttpContext, $"message must be at most {MaxMessageLength} characters.", ct);
            return;
        }

        var app = await queries.FindInCommunityAsync(req.CommunityId, req.ApplicationId, ct);
        if (app is null)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }
        var refusal = CastVoteEndpoint.StatusRefusal(app.Status);
        if (refusal is not null)
        {
            await ArcHttp.ConflictAsync(HttpContext, refusal.Value.code, refusal.Value.message, ct);
            return;
        }

        var caller = ArcQueries.CallerId(User);
        var request = new ArchitecturalInfoRequest
        {
            ApplicationId = app.Id,
            RequestedByUserId = caller,
            Message = message,
            RequestedAt = queries.Now
        };
        db.ArchitecturalInfoRequests.Add(request);
        await db.SaveChangesAsync(ct);

        var name = await db.Users.Where(u => u.Id == caller).Select(u => u.FirstName + " " + u.LastName).FirstAsync(ct);
        await SendAsync(new ArcInfoRequestCreatedDto(request.Id, name, request.Message, request.RequestedAt, app.DueDate),
            StatusCodes.Status201Created, ct);
    }
}

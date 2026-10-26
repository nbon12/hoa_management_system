using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Settings;

public sealed class ReplaceManagerRequest
{
    public Guid CommunityId { get; set; }
    public string? CurrentManagerUserId { get; set; }
    public string? ReplacementUserId { get; set; }
}

public sealed record ReplaceManagerResponse(MembershipDto Ended, MembershipDto Appointed);

/// <summary>
/// POST /communities/{communityId}/managers/replace — the one-action replace (030 FR-020, US2
/// #11), Company Administrators only. In one transaction under the community lock it grants (or
/// reactivates) an open-ended manager row for the replacement, then ends the current manager's.
/// Both changes are logged with the same Cause.
/// </summary>
public class ReplaceManagerEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, MembershipService memberships)
    : Endpoint<ReplaceManagerRequest, ReplaceManagerResponse>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/managers/replace");
        Description(x => x.WithName("ReplaceManager").WithTags("Community settings").RequireRateLimiting("board-writes"));
    }

    private const string NotACurrentManager = "That person isn't a current Community Manager of this community.";

    public override async Task HandleAsync(ReplaceManagerRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ReplaceLastManager, ScopeOperation.Write, ct), ct))
            return;

        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;
        if (string.IsNullOrWhiteSpace(req.CurrentManagerUserId))
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("currentManagerUserId", NotACurrentManager));
        if (string.IsNullOrWhiteSpace(req.ReplacementUserId) || req.CurrentManagerUserId == req.ReplacementUserId)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("replacementUserId",
                "Choose a different person from this community as the new manager."));
        if (req.ReplacementUserId == actorId)
            throw CompanyErrors.SelfGrantNotAllowed();

        var response = await CommunityLocks.RunAsync(db, req.CommunityId, async () =>
        {
            var current = await db.CommunityMemberships.FirstOrDefaultAsync(m => m.CommunityId == req.CommunityId
                && m.UserId == req.CurrentManagerUserId && m.Role == CommunityRole.CommunityManager
                && m.Status == MembershipStatus.Active, ct);
            if (current is null || !MembershipService.IsEffective(current, memberships.Today))
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("currentManagerUserId", NotACurrentManager));
            if (!await memberships.InPeopleListAsync(req.CommunityId, req.ReplacementUserId, ct))
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("replacementUserId", MembershipService.NotInPeopleList));

            var cause = $"replace:{Guid.NewGuid()}";
            var today = memberships.Today;
            var existing = await db.CommunityMemberships.FirstOrDefaultAsync(m => m.CommunityId == req.CommunityId
                && m.UserId == req.ReplacementUserId && m.Role == CommunityRole.CommunityManager, ct);
            Domain.Entities.CommunityMembership appointed;
            if (existing is not null && MembershipService.IsEffective(existing, today))
            {
                // Already a manager: make them open-ended, so the community keeps one after the end.
                appointed = existing;
                if (existing.EndDate is not null)
                {
                    existing.EndDate = null;
                    CompanyLog.MembershipChange(Logger, actorId, req.CommunityId, existing.Id, existing.UserId,
                        nameof(CommunityRole.CommunityManager), "replace-grant", cause, DateTimeOffset.UtcNow);
                }
            }
            else
                (appointed, _) = await memberships.GrantAsync(req.CommunityId, req.ReplacementUserId, CommunityRole.CommunityManager,
                    today, null, actorId, "replace-grant", cause, tolerateHeld: false, ct);
            await db.SaveChangesAsync(ct);
            await memberships.EndAsync(current, archived: false, actorId, "replace-end", cause, ct);

            var (endedName, _) = await memberships.NamesAsync(current.UserId, req.CommunityId, ct);
            var (appointedName, _) = await memberships.NamesAsync(appointed.UserId, req.CommunityId, ct);
            return new ReplaceManagerResponse(memberships.ToDto(current, endedName), memberships.ToDto(appointed, appointedName));
        }, ct);
        await SendOkAsync(response, ct);
    }
}

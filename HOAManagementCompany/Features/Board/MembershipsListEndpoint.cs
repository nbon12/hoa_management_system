using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// GET /api/v1/communities/{communityId}/memberships — the membership-admin list (025 US5; 030
// contract). Each row says whether it is open-ended and effective, and what the caller may do
// with it, with LAST_MANAGER as the reason when the open-ended-manager rule forbids it. Readable
// while archived by Company Administrators, who may then only end memberships (FR-012).
public class MembershipsListEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    MembershipService memberships,
    ILogger<MembershipsListEndpoint> logger)
    : Endpoint<MembershipsListQuery, PagedResponse<MembershipListItem>>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/memberships");
        Description(x => x.WithName("ListMemberships").WithTags("Board"));
    }

    public override async Task HandleAsync(MembershipsListQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageMemberships, ScopeOperation.Read, ct), ct))
            return;

        // FR-017: the roster exposes association-wide homeowner personal data —
        // record the access (actor, community, resource, UTC) as a sensitive event.
        logger.LogInformation(
            "BoardAssociationDataAccess: Actor={ActorId} Community={CommunityId} Resource={Resource} At={UtcNow:o}",
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "unknown",
            req.CommunityId, "communities/memberships", DateTimeOffset.UtcNow);

        var archived = await db.Communities.AnyAsync(c => c.Id == req.CommunityId && c.Status == CommunityStatus.Archived, ct);
        var mayWrite = !archived && await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageMemberships,
            ScopeOperation.Write, ct) == ScopeDecision.Allowed;
        var mayEndArchived = archived && await scope.DecideAsync(User, req.CommunityId,
            CommunityCapability.ManageCommunityLifecycle, ScopeOperation.Write, ct) == ScopeDecision.Allowed;
        var managers = await memberships.ManagerRowsAsync(req.CommunityId, ct);
        var today = memberships.Today;

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var baseQuery = db.CommunityMemberships.AsNoTracking().Where(m => m.CommunityId == req.CommunityId);
        var total = await baseQuery.CountAsync(ct);
        var rows = await baseQuery
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Skip(offset).Take(limit)
            .Select(m => new { Row = m, Name = m.User.FirstName + " " + m.User.LastName })
            .ToListAsync(ct);

        var items = rows.Select(r =>
        {
            var m = r.Row;
            var active = m.Status == MembershipStatus.Active && m.Role != CommunityRole.Resident;
            bool canEnd = false, canDowngrade = false, canEditEndDate = false;
            string? reason = null;
            if (active && archived)
                canEnd = mayEndArchived;
            else if (active && mayWrite && m.Role != CommunityRole.CommunityManager)
                (canEnd, canEditEndDate) = (true, true);
            else if (active && mayWrite)
            {
                canEnd = ManagerRule.Evaluate(managers, ManagerRule.Change.End(m.Id), today, false);
                canDowngrade = ManagerRule.Evaluate(managers, ManagerRule.Change.Downgrade(m.Id), today, false);
                canEditEndDate = ManagerRule.Evaluate(managers, ManagerRule.Change.SetEndDate(m.Id, today), today, false);
                reason = canEnd && canDowngrade && canEditEndDate ? null : "LAST_MANAGER";
            }
            return new MembershipListItem(m.Id, m.UserId, r.Name.Trim(), m.Role.ToString(), m.Status.ToString(),
                m.StartDate, m.EndDate, m.Status == MembershipStatus.Active && m.EndDate is null,
                MembershipService.IsEffective(m, today), canEnd, canDowngrade, canEditEndDate, reason);
        }).ToList();

        await SendOkAsync(new PagedResponse<MembershipListItem>(items, total, limit, offset), ct);
    }
}

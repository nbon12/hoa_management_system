using FastEndpoints;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// GET /api/v1/communities/{communityId}/memberships — the membership-admin list
// (User Story 5). Community Manager only, via the shared resolver (FR-012).
public class MembershipsListEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ILogger<MembershipsListEndpoint> logger)
    : Endpoint<MembershipsListQuery, PagedResponse<MembershipDto>>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/memberships");
        Description(x => x.WithName("ListMemberships").WithTags("Board"));
    }

    public override async Task HandleAsync(MembershipsListQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ManageMemberships, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        // FR-017: the roster exposes association-wide homeowner personal data —
        // record the access (actor, community, resource, UTC) as a sensitive event.
        logger.LogInformation(
            "BoardAssociationDataAccess: Actor={ActorId} Community={CommunityId} Resource={Resource} At={UtcNow:o}",
            BoardHttp.ActorId(User), req.CommunityId, "communities/memberships", DateTimeOffset.UtcNow);

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var baseQuery = db.CommunityMemberships.Where(m => m.CommunityId == req.CommunityId);
        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderBy(m => m.CreatedAt)
            .Skip(offset).Take(limit)
            .Select(m => new MembershipDto(
                m.Id, m.UserId, m.User.FirstName + " " + m.User.LastName,
                m.Role.ToString(), m.Status.ToString(), m.StartDate, m.EndDate))
            .ToListAsync(ct);

        await SendOkAsync(new PagedResponse<MembershipDto>(items, total, limit, offset), ct);
    }
}

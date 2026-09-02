using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// PATCH /api/v1/communities/{communityId}/memberships/{membershipId} — edit or end a
// membership (User Story 5, Scenarios 3 & 4). Community Manager only. Refuses to remove
// the last active Community Manager (FR-042, constitution §3). Each change is a
// security-sensitive event (FR-042, §7). Takes effect on the member's next request.
public class MembershipUpdateEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ILogger<MembershipUpdateEndpoint> logger)
    : Endpoint<MembershipUpdateRequest, MembershipDto>
{
    public override void Configure()
    {
        Patch("/communities/{communityId}/memberships/{membershipId}");
        Description(x => x.WithName("UpdateMembership").WithTags("Board"));
    }

    public override async Task HandleAsync(MembershipUpdateRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        try
        {
            if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ManageMemberships, ct))
            {
                await BoardHttp.ForbiddenAsync(HttpContext, ct);
                return;
            }

            var m = await db.CommunityMemberships
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == req.MembershipId && x.CommunityId == req.CommunityId, ct);
            if (m is null)
                throw new DomainException("NOT_FOUND", "Membership not found in this community.", 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var newRole = req.Role ?? m.Role;
            var newStatus = req.Status ?? m.Status;
            var newEnd = req.EndDate ?? m.EndDate;

            static bool IsActiveManager(CommunityRole r, MembershipStatus s, DateOnly? end, DateOnly today) =>
                r == CommunityRole.CommunityManager && s == MembershipStatus.Active && (end is null || end >= today);

            // C3 / §3: never leave a community with zero active Community Managers.
            if (IsActiveManager(m.Role, m.Status, m.EndDate, today)
                && !IsActiveManager(newRole, newStatus, newEnd, today))
            {
                var otherActiveManagers = await db.CommunityMemberships.CountAsync(
                    x => x.CommunityId == req.CommunityId
                      && x.Id != m.Id
                      && x.Role == CommunityRole.CommunityManager
                      && x.Status == MembershipStatus.Active
                      && (x.EndDate == null || x.EndDate >= today), ct);
                if (otherActiveManagers == 0)
                    throw new DomainException("LAST_MANAGER",
                        "A community must keep at least one active Community Manager.", 422);
            }

            m.Role = newRole;
            m.Status = newStatus;
            if (req.EndDate is not null) m.EndDate = req.EndDate;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "BoardMembershipChange edit: Actor={ActorId} Community={CommunityId} Membership={MembershipId} TargetUser={TargetUserId} Role={Role} Status={Status} EndDate={EndDate} At={UtcNow:o}",
                BoardHttp.ActorId(User), req.CommunityId, m.Id, m.UserId, m.Role, m.Status, m.EndDate, DateTimeOffset.UtcNow);

            await SendOkAsync(new MembershipDto(
                m.Id, m.UserId, m.User.FirstName + " " + m.User.LastName, m.Role.ToString(),
                m.Status.ToString(), m.StartDate, m.EndDate), ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
            await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

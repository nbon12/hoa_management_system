using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// POST /api/v1/communities/{communityId}/memberships — grant a membership
// (User Story 5, Scenario 1). Community Manager only. Every grant is recorded as a
// security-sensitive event (FR-042, constitution §7).
public class MembershipCreateEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ILogger<MembershipCreateEndpoint> logger)
    : Endpoint<MembershipCreateRequest, MembershipDto>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/memberships");
        Description(x => x.WithName("CreateMembership").WithTags("Board"));
    }

    public override async Task HandleAsync(MembershipCreateRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        try
        {
            if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ManageMemberships, ct))
            {
                await BoardHttp.ForbiddenAsync(HttpContext, ct);
                return;
            }

            if (req.Role == CommunityRole.Resident)
                throw new DomainException("VALIDATION_ERROR", "Role must not be Resident.", 422);
            if (req.EndDate is { } end && end < req.StartDate)
                throw new DomainException("VALIDATION_ERROR", "End date cannot precede start date.", 422);
            // One round trip does double duty: the display name for the response, and —
            // when no row comes back — the "unknown user" rejection, in its original
            // position in the validation order. This relies on FirstName/LastName being
            // NOT NULL (both are IsRequired): the projection concatenates them without a
            // COALESCE, so a nullable name column would make an existing user's name come
            // back null and be misreported as "Unknown user."
            var name = await db.Users.Where(u => u.Id == req.UserId)
                .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct);
            if (name is null)
                throw new DomainException("VALIDATION_ERROR", "Unknown user.", 422);
            if (await db.CommunityMemberships.AnyAsync(
                    m => m.UserId == req.UserId && m.CommunityId == req.CommunityId && m.Role == req.Role, ct))
                throw new DomainException("VALIDATION_ERROR", "That user already holds this role in this community.", 422);

            var membership = new CommunityMembership
            {
                UserId = req.UserId,
                CommunityId = req.CommunityId,
                Role = req.Role,
                Status = MembershipStatus.Active,
                StartDate = req.StartDate,
                EndDate = req.EndDate
            };
            db.CommunityMemberships.Add(membership);
            await db.SaveChangesAsync(ct);

            // FR-042 / §7 sensitive event.
            logger.LogInformation(
                "BoardMembershipChange grant: Actor={ActorId} Community={CommunityId} TargetUser={TargetUserId} Role={Role} At={UtcNow:o}",
                BoardHttp.ActorId(User), req.CommunityId, req.UserId, req.Role, DateTimeOffset.UtcNow);

            await SendAsync(new MembershipDto(
                membership.Id, membership.UserId, name, membership.Role.ToString(),
                membership.Status.ToString(), membership.StartDate, membership.EndDate), 201, ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
            await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

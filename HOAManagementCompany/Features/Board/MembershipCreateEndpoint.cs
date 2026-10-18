using System.Net.Mail;
using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Infrastructure.Persistence;

namespace HOAManagementCompany.Features.Board;

public sealed record MembershipInvitationResponse(Guid InvitationId, string State, DateTimeOffset? ExpiresAt);

// POST /api/v1/communities/{communityId}/memberships — appoint a Community Manager, Board
// Member or Accountant (025 US5; 030 FR-018–FR-021, contracts/community-settings.md). With a
// userId the person must already be in the community's people list and the role is granted at
// once (reactivating a previous row, R7); with an email a membership invitation is sent, with the
// same response whether or not the email has an account. Community Manager grants need
// ManageCommunityManagers and go through the open-ended-manager rule under the community lock.
// Every change is a sensitive event (FR-023).
public class MembershipCreateEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ICompanyContext companyContext,
    MembershipService memberships,
    InvitationService invitations)
    : Endpoint<MembershipCreateRequest, object>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/memberships");
        // 030 FR-041a: per-user write limit, which 025 lacked here.
        Description(x => x.WithName("CreateMembership").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(MembershipCreateRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var capability = req.Role == CommunityRole.CommunityManager
            ? CommunityCapability.ManageCommunityManagers
            : CommunityCapability.ManageMemberships;
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, capability, ScopeOperation.Write, ct), ct))
            return;

        if (req.Role is not (CommunityRole.CommunityManager or CommunityRole.BoardMember or CommunityRole.Accountant))
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("role", "Choose Community Manager, Board Member or Accountant."));
        var hasUser = !string.IsNullOrWhiteSpace(req.UserId);
        var hasEmail = !string.IsNullOrWhiteSpace(req.Email);
        if (hasUser == hasEmail)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError(hasUser ? "email" : "userId",
                "Choose someone from this community, or invite them by email."));

        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;
        if (hasEmail)
        {
            if (!MailAddress.TryCreate(req.Email!.Trim(), out var parsed) || parsed.Address != req.Email.Trim())
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("email", "Enter a valid email address."));
            var companyId = await companyContext.ForCommunityAsync(req.CommunityId, ct)
                            ?? throw CompanyErrors.Forbidden();
            var invitation = await invitations.CreateAsync(new NewInvitation(InvitationKind.Membership, companyId,
                req.CommunityId, null, req.Role, req.EndDate, req.Email), actorId, ct);
            await SendAsync(new MembershipInvitationResponse(invitation.Id, invitation.State.ToString(), invitation.ExpiresAt),
                StatusCodes.Status201Created, ct);
            return;
        }

        // FR-003a: nobody grants themself a role.
        if (req.UserId == actorId)
            throw CompanyErrors.SelfGrantNotAllowed();
        var startDate = req.StartDate ?? memberships.Today;
        if (req.EndDate is { } end && end < startDate)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("endDate", "The end date can't be before the start date."));

        var dto = await CommunityLocks.RunAsync(db, req.CommunityId, async () =>
        {
            // The message doesn't say whether the id exists.
            if (!await memberships.InPeopleListAsync(req.CommunityId, req.UserId!, ct))
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("userId", MembershipService.NotInPeopleList));
            var (row, _) = await memberships.GrantAsync(req.CommunityId, req.UserId!, req.Role, startDate, req.EndDate,
                actorId, "grant", null, tolerateHeld: false, ct);
            await db.SaveChangesAsync(ct);
            var (name, _) = await memberships.NamesAsync(row.UserId, req.CommunityId, ct);
            return memberships.ToDto(row, name);
        }, ct);
        await SendAsync(dto, StatusCodes.Status201Created, ct);
    }
}

using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// The membership offer (030 FR-018, FR-021, FR-022): Community Manager, Board Member or
/// Accountant, with an optional term end date. Accepting creates or reactivates the membership,
/// active from the acceptance date; a Community Manager offer is checked against the
/// open-ended-manager rule again at acceptance (R7).
/// </summary>
public sealed class MembershipOfferHandler(ApplicationDbContext db, MembershipService memberships) : IInvitationOfferHandler
{
    private static readonly CommunityRole[] OfferedRoles =
        [CommunityRole.CommunityManager, CommunityRole.BoardMember, CommunityRole.Accountant];

    public InvitationKind Kind => InvitationKind.Membership;

    public async Task ValidateCreateAsync(Invitation invitation, CancellationToken ct)
    {
        if (invitation.Role is not { } role || !OfferedRoles.Contains(role))
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("role",
                "Choose Community Manager, Board Member or Accountant."));
        if (invitation.TermEndDate is { } end && end < memberships.Today)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("endDate", "The end date can't be in the past."));

        var normalized = invitation.NormalizedEmail.ToUpperInvariant();
        if (invitation.SenderUserId is { } senderId
            && await db.Users.AnyAsync(u => u.Id == senderId && u.NormalizedEmail == normalized, ct))
            throw CompanyErrors.SelfGrantNotAllowed();

        // FR-022: naming the email reveals only a role in the sender's own community.
        var today = memberships.Today;
        if (await db.CommunityMemberships.AnyAsync(m => m.CommunityId == invitation.CommunityId && m.Role == role
                                                        && m.User.NormalizedEmail == normalized
                                                        && m.Status == MembershipStatus.Active
                                                        && (m.EndDate == null || m.EndDate >= today), ct))
        {
            var community = await db.Communities.Where(c => c.Id == invitation.CommunityId).Select(c => c.CommunityName).FirstAsync(ct);
            throw CompanyErrors.AlreadyMember($"{invitation.Email} is already {MembershipService.WithArticle(role)} of {community}.");
        }

        // An end-dated manager invitation is allowed only while another open-ended manager exists.
        if (role == CommunityRole.CommunityManager && invitation.TermEndDate is not null
            && !ManagerRule.HasOpenEndedManager(await memberships.ManagerRowsAsync(invitation.CommunityId!.Value, ct)))
        {
            var community = await db.Communities.Where(c => c.Id == invitation.CommunityId).Select(c => c.CommunityName).FirstAsync(ct);
            throw CompanyErrors.LastManager(
                $"{community} has no Community Manager with no end date yet, so its first manager can't have an end date. Invite them with no end date.");
        }
    }

    public async Task<bool> ApplyAsync(Invitation invitation, string userId, CancellationToken ct)
    {
        var (_, alreadyHeld) = await memberships.GrantAsync(invitation.CommunityId!.Value, userId, invitation.Role!.Value,
            memberships.Today, invitation.TermEndDate, userId, "grant", $"invitation:{invitation.Id}", tolerateHeld: true, ct);
        return alreadyHeld;
    }
}

using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// The residency offer (030 FR-024–FR-024b): a home in the community, for one email. Any number
/// of residents may share a property, so accepting only adds the caller's link (FR-024e). The
/// invitation is Scheduled while the community is Onboarding (<see cref="InvitationService"/>),
/// and its sender's authority is <c>ManageResidents</c> (<see cref="Board.SenderAuthority"/>).
/// </summary>
public sealed class ResidencyOfferHandler(ApplicationDbContext db, TimeProvider clock, ILogger<ResidencyOfferHandler> logger)
    : IInvitationOfferHandler
{
    public InvitationKind Kind => InvitationKind.Residency;

    public async Task ValidateCreateAsync(Invitation invitation, CancellationToken ct)
    {
        var address = await db.Properties
                          .Where(p => p.Id == invitation.PropertyId && p.CommunityId == invitation.CommunityId)
                          .Select(p => p.Address)
                          .FirstOrDefaultAsync(ct)
                      ?? throw CompanyErrors.Forbidden();

        // US8-S17: nobody gives themself a home, whatever the case of the address.
        var normalized = invitation.NormalizedEmail.ToUpperInvariant();
        if (invitation.SenderUserId is { } senderId
            && await db.Users.AnyAsync(u => u.Id == senderId && u.NormalizedEmail == normalized, ct))
            throw CompanyErrors.SelfGrantNotAllowed();

        if (await db.UserProperties.AnyAsync(up => up.PropertyId == invitation.PropertyId
                                                   && up.User.NormalizedEmail == normalized, ct))
            throw CompanyErrors.AlreadyResident($"{invitation.Email} is already a resident of {address}.");
    }

    public async Task<bool> ApplyAsync(Invitation invitation, string userId, CancellationToken ct)
    {
        var propertyId = invitation.PropertyId!.Value;
        if (await db.UserProperties.AnyAsync(up => up.PropertyId == propertyId && up.UserId == userId, ct))
            return true;

        var now = clock.GetUtcNow();
        db.UserProperties.Add(new UserProperty { Id = Guid.NewGuid(), UserId = userId, PropertyId = propertyId, LinkedAt = now });
        await db.SaveChangesAsync(ct);
        CompanyLog.ResidentLinkChange(logger, userId, invitation.CommunityId!.Value, propertyId, userId, "Linked",
            $"invitation:{invitation.Id}", now);
        return false;
    }
}

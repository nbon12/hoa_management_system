using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// The Company Administrator offer (030 FR-037, FR-038): accepting creates a live grant with
/// <c>Source = Invitation</c>. Someone who already holds a live grant gets nothing new.
/// </summary>
public sealed class CompanyAdministratorOfferHandler(
    ApplicationDbContext db,
    TimeProvider clock,
    ILogger<CompanyAdministratorOfferHandler> logger) : IInvitationOfferHandler
{
    public InvitationKind Kind => InvitationKind.CompanyAdministrator;

    public async Task ValidateCreateAsync(Invitation invitation, CancellationToken ct)
    {
        var normalized = invitation.NormalizedEmail.ToUpperInvariant();
        if (invitation.SenderUserId is { } senderId
            && await db.Users.AnyAsync(u => u.Id == senderId && u.NormalizedEmail == normalized, ct))
            throw CompanyErrors.SelfGrantNotAllowed(companyAdministratorPage: true);

        // The current administrators are listed on the same page, so naming the email reveals nothing.
        if (await db.CompanyAdministratorGrants.AnyAsync(g =>
                g.CompanyId == invitation.CompanyId && g.RevokedAt == null && g.User.NormalizedEmail == normalized, ct))
            throw CompanyErrors.AlreadyMember($"{invitation.Email} is already a Company Administrator.");
    }

    public async Task<bool> ApplyAsync(Invitation invitation, string userId, CancellationToken ct)
    {
        if (await db.CompanyAdministratorGrants.AnyAsync(g =>
                g.CompanyId == invitation.CompanyId && g.UserId == userId && g.RevokedAt == null, ct))
            return true;

        var now = clock.GetUtcNow();
        db.CompanyAdministratorGrants.Add(new CompanyAdministratorGrant
        {
            Id = Guid.NewGuid(),
            CompanyId = invitation.CompanyId,
            UserId = userId,
            Source = CompanyGrantSource.Invitation,
            GrantedByUserId = invitation.SenderUserId,
            GrantedAt = now,
            InvitationId = invitation.Id,
        });
        CompanyLog.CompanyAdminChange(logger, userId, invitation.CompanyId, userId, invitation.Id, "Granted", now);
        return false;
    }
}

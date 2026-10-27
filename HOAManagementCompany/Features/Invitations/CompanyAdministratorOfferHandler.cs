using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// The Company Administrator offer (030 FR-037, FR-038): accepting creates a live grant. Its source
/// is <c>Invitation</c> when an administrator sent it. The setup step sends with no sender, and its
/// invitation grants as its direct grant does, judged when the grant is made: <c>Recovery</c> when
/// other administrators hold the role, and then they are told, otherwise <c>Setup</c>. Someone who
/// already holds a live grant gets nothing new.
/// </summary>
public sealed class CompanyAdministratorOfferHandler(
    ApplicationDbContext db,
    CompanyEmailRenderer emails,
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
        var others = invitation.SenderUserId is null
            ? await db.CompanyAdministratorGrants
                .Where(g => g.CompanyId == invitation.CompanyId && g.RevokedAt == null)
                .Select(g => new { g.UserId, g.User.Email })
                .ToListAsync(ct)
            : [];
        var source = invitation.SenderUserId is not null ? CompanyGrantSource.Invitation
            : others.Count > 0 ? CompanyGrantSource.Recovery
            : CompanyGrantSource.Setup;

        // FR-038: a recovery grant is never silent. The key is the invitation's, so administrators
        // already told when a --recover run sent it aren't told twice.
        foreach (var admin in others.Where(a => !string.IsNullOrEmpty(a.Email)))
        {
            var row = emails.AdminRecovery(admin.UserId, admin.Email!, invitation.Id, invitation.Email, now);
            if (!await db.OutboxMessages.AnyAsync(m => m.DedupKey == row.DedupKey, ct))
                db.OutboxMessages.Add(row);
        }

        db.CompanyAdministratorGrants.Add(new CompanyAdministratorGrant
        {
            Id = Guid.NewGuid(),
            CompanyId = invitation.CompanyId,
            UserId = userId,
            Source = source,
            GrantedByUserId = invitation.SenderUserId,
            GrantedAt = now,
            InvitationId = invitation.Id,
        });
        CompanyLog.CompanyAdminChange(logger, userId, invitation.CompanyId, userId, invitation.Id,
            source == CompanyGrantSource.Recovery ? "Recovery" : "Granted", now);
        return false;
    }
}

using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

/// <summary>
/// Whether an invitation's sender could still send it (030 FR-022; research R6). Checked at
/// acceptance and when the activation fan-out sends scheduled invitations, by re-running the
/// resolver for the capability that sends that kind. When a change takes authority away — a
/// manager membership ended or downgraded, a Company Administrator grant revoked — the change
/// calls <see cref="RevokeOrphanedAsync"/> in its own transaction.
/// </summary>
public sealed class SenderAuthority(
    ApplicationDbContext db,
    ICommunityScopeResolver resolver,
    TimeProvider clock,
    ILogger<SenderAuthority> logger)
{
    /// <summary>The capability that sends (and resends and revokes) this kind of invitation.</summary>
    public static CommunityCapability CapabilityFor(InvitationKind kind, CommunityRole? role) => kind switch
    {
        InvitationKind.Membership when role == CommunityRole.CommunityManager => CommunityCapability.ManageCommunityManagers,
        InvitationKind.Membership => CommunityCapability.ManageMemberships,
        InvitationKind.Residency => CommunityCapability.ManageResidents,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), "Company Administrator invitations use the company grant."),
    };

    /// <summary>
    /// True when <paramref name="userId"/> could send <paramref name="invitation"/> now. A
    /// setup-step invitation (no sender) always passes, until a Company Administrator resends it.
    /// </summary>
    public async Task<bool> CouldSendAsync(string? userId, Invitation invitation, CancellationToken ct)
    {
        if (userId is null)
            return invitation.Kind == InvitationKind.CompanyAdministrator;
        return await DecideAsync(userId, invitation, ct) == ScopeDecision.Allowed;
    }

    /// <summary>
    /// Whether <paramref name="userId"/> may send, resend or revoke <paramref name="invitation"/> now,
    /// as a write: forbidden, archived or allowed, as the resolver decides.
    /// </summary>
    public async Task<ScopeDecision> DecideAsync(string userId, Invitation invitation, CancellationToken ct)
    {
        var principal = Principal(userId);
        if (invitation.Kind == InvitationKind.CompanyAdministrator)
            return await resolver.IsCompanyAdministratorAsync(principal, invitation.CompanyId, ct)
                ? ScopeDecision.Allowed
                : ScopeDecision.Forbidden;

        return await resolver.DecideAsync(principal, invitation.CommunityId!.Value,
            CapabilityFor(invitation.Kind, invitation.Role), ScopeOperation.Write, ct);
    }

    /// <summary>
    /// Revokes, in the caller's transaction, every scheduled or pending invitation
    /// <paramref name="userId"/> sent and can no longer send — in <paramref name="communityId"/>, or
    /// anywhere when it is null. Call it after saving the change that took the authority away, so
    /// the resolver sees it. Each revoke is logged with reason <c>SenderLostAuthority</c>, the
    /// change's actor and its cause. The caller saves. Returns how many were revoked.
    /// </summary>
    public async Task<int> RevokeOrphanedAsync(string userId, Guid? communityId, string actorId, string cause,
        CancellationToken ct)
    {
        // Row locks, because a Company Administrator revoke holds only the company lock while it
        // touches invitations in every community.
        var open = await db.Invitations
            .FromSqlInterpolated($"""
                SELECT * FROM "Invitations"
                WHERE "SenderUserId" = {userId} AND "State" IN ('Scheduled', 'Pending')
                FOR UPDATE
                """)
            .Where(i => communityId == null || i.CommunityId == communityId)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        var revoked = 0;
        foreach (var invitation in open)
        {
            if (await CouldSendAsync(userId, invitation, ct))
                continue;
            invitation.State = InvitationState.Revoked;
            invitation.RevokedAt = now;
            invitation.RevokedByUserId = null;
            invitation.RevokeReason = InvitationRevokeReason.SenderLostAuthority;
            CompanyLog.InvitationChange(logger, actorId, invitation.CommunityId, invitation.PropertyId, invitation.Id,
                await Invitations.InvitationService.InviteeUserIdAsync(db, invitation, ct),
                invitation.Kind.ToString(), "Revoked", nameof(InvitationRevokeReason.SenderLostAuthority), cause, now);
            revoked++;
        }
        return revoked;
    }

    private static ClaimsPrincipal Principal(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim("sub", userId)], "invitation-sender"));
}

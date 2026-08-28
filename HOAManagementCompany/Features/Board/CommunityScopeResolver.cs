using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// <!-- REPOWISE:START domain=board -->
// The single community-scope resolver (spec FR-012, FR-014). Authorization is
// decided server-side from persisted membership, strictly per Community row
// (no master/sub cascade) and as the union across a caller's role rows
// (Clarifications 2026-08-23). 95%-coverage-critical.
// <!-- REPOWISE:END -->

public class CommunityScopeResolver(ApplicationDbContext db) : ICommunityScopeResolver
{
    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        Guid communityId,
        CommunityCapability capability,
        CancellationToken ct = default)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Only THIS community's rows — the parent/child relation is never traversed
        // (Clarifications 2026-08-23). CommunityMembership.IsEffectiveAsOf carries the
        // effective-permission rule, including the community-must-be-Active clause: an
        // inactive/offboarded community (and, via the FK, a community that does not exist)
        // matches no rows, so the caller fails closed below.
        var roles = await db.CommunityMemberships
            .Where(CommunityMembership.IsEffectiveAsOf(today))
            .Where(m => m.UserId == userId && m.CommunityId == communityId)
            .Select(m => m.Role)
            .ToListAsync(ct);

        // Union: allow if ANY active role row grants the capability.
        return roles.Any(role => RoleGrants(role, capability));
    }

    private static bool RoleGrants(CommunityRole role, CommunityCapability capability) => capability switch
    {
        CommunityCapability.ViewAssociationData =>
            role is CommunityRole.BoardMember
                 or CommunityRole.CommunityManager
                 or CommunityRole.Accountant,
        CommunityCapability.ManageMemberships =>
            role is CommunityRole.CommunityManager,
        _ => false
    };
}

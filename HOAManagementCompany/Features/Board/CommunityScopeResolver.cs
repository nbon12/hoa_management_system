using System.Security.Claims;
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

        // An inactive/offboarded community confers no access to anyone, regardless
        // of the membership's own status (spec Edge Cases).
        var communityActive = await db.Communities
            .AnyAsync(c => c.Id == communityId && c.Status == CommunityStatus.Active, ct);
        if (!communityActive)
            return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Only THIS community's rows — the parent/child relation is never traversed
        // (Clarifications 2026-08-23). Effective-permission rule per data-model.md:
        // Status == Active AND (EndDate is null OR EndDate >= today UTC).
        var roles = await db.CommunityMemberships
            .Where(m => m.UserId == userId
                     && m.CommunityId == communityId
                     && m.Status == MembershipStatus.Active
                     && (m.EndDate == null || m.EndDate >= today))
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

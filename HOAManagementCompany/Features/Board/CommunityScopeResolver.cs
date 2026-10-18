using System.Diagnostics;
using System.Security.Claims;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// <!-- REPOWISE:START domain=board -->
// The single community-scope resolver (025 FR-012, FR-014; 030 FR-004, FR-004a). Authorization is
// decided server-side from persisted state, strictly per Community row (no master/sub cascade)
// and as the union across a caller's role rows (025 Clarifications 2026-08-23):
//   1. Authorize as if the community were Active, from the caller's effective memberships or a
//      live Company Administrator grant for the community's company. A failure is Forbidden,
//      identical to a nonexistent community.
//   2. Apply the status: Onboarding and Active are Allowed; Archived is Archived, except that a
//      Company Administrator still reads the settings sections and may restore and end memberships.
// The Company Administrator grant never confers ViewAssociationData or the 027 architectural
// application capabilities (030 FR-003). Company-level capabilities (FR-004 b and c) come from the
// grant or, for adding a community and reading the defaults, an effective manager membership.
// 95%-coverage-critical.
// <!-- REPOWISE:END -->

public class CommunityScopeResolver(ApplicationDbContext db) : ICommunityScopeResolver
{
    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        Guid communityId,
        CommunityCapability capability,
        CancellationToken ct = default) =>
        await DecideAsync(user, communityId, capability, ScopeOperation.Read, ct) == ScopeDecision.Allowed;

    public async Task<ScopeDecision> DecideAsync(
        ClaimsPrincipal user,
        Guid communityId,
        CommunityCapability capability,
        ScopeOperation operation,
        CancellationToken ct = default)
    {
        // Spec Observability: community and capability tag the request span, so backend Sentry
        // events (which read the OpenTelemetry pipeline) carry them. IDs and enum names only.
        Activity.Current?.SetTag("community.id", communityId.ToString());
        Activity.Current?.SetTag("capability", capability.ToString());

        var userId = UserId(user);
        if (string.IsNullOrEmpty(userId))
            return ScopeDecision.Forbidden;

        var community = await db.Communities
            .Where(c => c.Id == communityId)
            .Select(c => new { c.Status, c.ManagementCompanyId })
            .FirstOrDefaultAsync(ct);
        if (community is null)
            return ScopeDecision.Forbidden;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Only THIS community's rows — the parent/child relation is never traversed. Effective
        // membership: Status == Active AND (EndDate is null OR EndDate >= today UTC). The
        // community's status is not considered in step 1 (FR-004a).
        var roles = await db.CommunityMemberships
            .Where(m => m.UserId == userId
                     && m.CommunityId == communityId
                     && m.Status == MembershipStatus.Active
                     && (m.EndDate == null || m.EndDate >= today))
            .Select(m => m.Role)
            .ToListAsync(ct);
        var viaMembership = roles.Any(role => RoleGrants(role, capability));

        var viaGrant = AdministratorGrants(capability)
            && await db.CompanyAdministratorGrants.AnyAsync(
                g => g.UserId == userId && g.CompanyId == community.ManagementCompanyId && g.RevokedAt == null, ct);

        if (!viaMembership && !viaGrant)
            return ScopeDecision.Forbidden;

        if (community.Status != CommunityStatus.Archived)
            return ScopeDecision.Allowed;

        // Archived (FR-012): readable, read-only, to Company Administrators, who may also restore and
        // end memberships. Every other operation is refused as archived — never as forbidden, since
        // the caller is authorized (FR-004a order).
        return viaGrant && (operation == ScopeOperation.Read || capability == CommunityCapability.ManageCommunityLifecycle)
            ? ScopeDecision.Allowed
            : ScopeDecision.Archived;
    }

    public async Task<bool> CanAccessCompanyAsync(
        ClaimsPrincipal user,
        CompanyCapability capability,
        CancellationToken ct = default)
    {
        var userId = UserId(user);
        if (string.IsNullOrEmpty(userId))
            return false;

        if (await db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == userId && g.RevokedAt == null, ct))
            return true;

        // FR-004 (b): adding a community and reading the defaults are shared with any effective
        // Community Manager of an Onboarding or Active community.
        if (capability is not (CompanyCapability.CreateCommunity or CompanyCapability.ViewCompanyDefaults))
            return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.CommunityMemberships.AnyAsync(m =>
            m.UserId == userId
            && m.Role == CommunityRole.CommunityManager
            && m.Status == MembershipStatus.Active
            && (m.EndDate == null || m.EndDate >= today)
            && m.Community.Status != CommunityStatus.Archived, ct);
    }

    public async Task<bool> IsCompanyAdministratorAsync(
        ClaimsPrincipal user,
        Guid? companyId = null,
        CancellationToken ct = default)
    {
        var userId = UserId(user);
        if (string.IsNullOrEmpty(userId))
            return false;
        return await db.CompanyAdministratorGrants.AnyAsync(
            g => g.UserId == userId && g.RevokedAt == null && (companyId == null || g.CompanyId == companyId), ct);
    }

    private static string? UserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;

    private static bool RoleGrants(CommunityRole role, CommunityCapability capability) => capability switch
    {
        CommunityCapability.ViewAssociationData =>
            role is CommunityRole.BoardMember or CommunityRole.CommunityManager or CommunityRole.Accountant,
        CommunityCapability.ViewArchitecturalApplications =>
            role is CommunityRole.BoardMember or CommunityRole.CommunityManager,
        CommunityCapability.VoteArchitecturalApplications =>
            role is CommunityRole.BoardMember,
        CommunityCapability.ViewArcSettings =>
            role is CommunityRole.BoardMember or CommunityRole.CommunityManager,
        CommunityCapability.ManageMemberships
            or CommunityCapability.ManageArchitecturalReview
            or CommunityCapability.ManageCommunityManagers
            or CommunityCapability.EditCommunityProfile
            or CommunityCapability.ManageResidents
            or CommunityCapability.ManageArcSettings
            or CommunityCapability.ViewCommunitySettings =>
            role is CommunityRole.CommunityManager,
        // ReplaceLastManager and ManageCommunityLifecycle are Company Administrator only.
        _ => false
    };

    // 030 R2: what the Company Administrator grant confers in a community of its company.
    private static bool AdministratorGrants(CommunityCapability capability) => capability is
        CommunityCapability.ManageMemberships
        or CommunityCapability.ManageCommunityManagers
        or CommunityCapability.ReplaceLastManager
        or CommunityCapability.EditCommunityProfile
        or CommunityCapability.ManageResidents
        or CommunityCapability.ViewArcSettings
        or CommunityCapability.ManageArcSettings
        or CommunityCapability.ManageCommunityLifecycle
        or CommunityCapability.ViewCommunitySettings;
}

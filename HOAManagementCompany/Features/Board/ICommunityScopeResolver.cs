using System.Security.Claims;

namespace HOAManagementCompany.Features.Board;

// Capabilities a community-scoped board endpoint can require (spec FR-012).
// Kept deliberately small; sibling specs 2-6 extend this set as they add surfaces.
public enum CommunityCapability
{
    // Read association-wide member/financial data (board members, managers, accountants).
    ViewAssociationData,

    // Create/edit CommunityMembership records (community managers only — FR-042).
    ManageMemberships,

    // 027: list, detail and attachment links for architectural applications
    // (board members and community managers; not accountants).
    ViewArchitecturalApplications,

    // 027: cast votes and request info on architectural applications (board members only).
    VoteArchitecturalApplications,

    // 027: record outcomes, resend owner emails, edit ARC settings (community managers only).
    ManageArchitecturalReview
}

// The single server-side community-scope resolver (spec FR-012). Every board-side
// endpoint across specs 2-6 MUST authorize through this rather than implementing
// its own check.
public interface ICommunityScopeResolver
{
    // Returns true iff any of the caller's ACTIVE membership rows in the target
    // community confers `capability` (union across role rows — Clarifications
    // 2026-08-23). Decides strictly per Community row and never traverses the
    // parent/child hierarchy. The client's current mode is never an input (FR-014).
    Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        Guid communityId,
        CommunityCapability capability,
        CancellationToken ct = default);
}

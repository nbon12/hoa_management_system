using System.Security.Claims;

namespace HOAManagementCompany.Features.Board;

// Capabilities a community-scoped endpoint can require (025 FR-012). Sibling specs extend this
// set as they add surfaces; 030 research R2 lists who holds each one.
public enum CommunityCapability
{
    // Read association-wide member/financial data (board members, managers, accountants).
    ViewAssociationData,

    // Board Member and Accountant memberships, the people list and their invitations
    // (community managers and Company Administrators — 025 FR-042, 030 FR-021).
    ManageMemberships,

    // 027: list, detail and attachment links for architectural applications
    // (board members and community managers; not accountants).
    ViewArchitecturalApplications,

    // 027: cast votes and request info on architectural applications (board members only).
    VoteArchitecturalApplications,

    // 027: record outcomes and resend owner emails (community managers only).
    ManageArchitecturalReview,

    // 030: appoint, end and downgrade Community Managers, and their invitations.
    ManageCommunityManagers,

    // 030 FR-020: replace the last manager in one action (Company Administrators only).
    ReplaceLastManager,

    // 030 FR-017: the Profile fields.
    EditCommunityProfile,

    // 030 FR-024: the Residents area — list, invite, resend, revoke, remove.
    ManageResidents,

    // 030 FR-030: read ARC settings (Board Members keep their 027 read access).
    ViewArcSettings,

    // 030 FR-030/FR-031: edit and reset ARC settings.
    ManageArcSettings,

    // 030 FR-010/FR-016: activate, archive, restore, change parent, and end memberships while
    // archived (Company Administrators only).
    ManageCommunityLifecycle,

    // 030 FR-029: the Community settings page shell and its read endpoints.
    ViewCommunitySettings
}

// 030 R2: company-level capabilities, decided from the Company Administrator grant and, for the
// shared ones, an effective Community Manager membership (FR-004 checks b and c).
public enum CompanyCapability
{
    CreateCommunity,
    ViewCompanyDefaults,
    ManageCompanyDefaults,
    ManageCompanyAdministrators,
    ViewPortfolio
}

// 030 FR-004a: the outcome of a community-scoped check, so every endpoint returns the right
// refusal from one place — 403 FORBIDDEN, 409 COMMUNITY_ARCHIVED or 409 COMMUNITY_NOT_ACTIVE.
public enum ScopeDecision
{
    Allowed,
    Forbidden,
    Archived,
    NotActive
}

public enum ScopeOperation
{
    Read,
    Write
}

// The single server-side community-scope resolver (025 FR-012). Every board-side and company
// endpoint MUST authorize through this rather than implementing its own check.
public interface ICommunityScopeResolver
{
    // True iff DecideAsync(..., Read) is Allowed: the caller may read with this capability in an
    // Onboarding or Active community. Kept for callers that only need a yes or no.
    Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        Guid communityId,
        CommunityCapability capability,
        CancellationToken ct = default);

    // 030 FR-004a: (1) authorize as if the community were Active, from the caller's effective
    // memberships or the Company Administrator grant for the community's company — a failure is
    // Forbidden, identical to a nonexistent community; (2) apply the community's status. Decided
    // strictly per Community row; the parent link is never traversed (025 Clarifications).
    Task<ScopeDecision> DecideAsync(
        ClaimsPrincipal user,
        Guid communityId,
        CommunityCapability capability,
        ScopeOperation operation,
        CancellationToken ct = default);

    // 030 FR-004 (b) and (c): company-level capabilities for the caller's company.
    Task<bool> CanAccessCompanyAsync(
        ClaimsPrincipal user,
        CompanyCapability capability,
        CancellationToken ct = default);

    // True when the caller holds a live Company Administrator grant — for the given company, or
    // for any company when companyId is null.
    Task<bool> IsCompanyAdministratorAsync(
        ClaimsPrincipal user,
        Guid? companyId = null,
        CancellationToken ct = default);
}

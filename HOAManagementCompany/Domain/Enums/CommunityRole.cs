namespace HOAManagementCompany.Domain.Enums;

// Roles a user may hold within a single community (spec FR-008).
// Architectural review is a BoardMember capability — there is no separate
// committee-member role (spec Clarifications, 2026-08-20).
public enum CommunityRole
{
    Resident,
    BoardMember,
    CommunityManager,
    Accountant
}

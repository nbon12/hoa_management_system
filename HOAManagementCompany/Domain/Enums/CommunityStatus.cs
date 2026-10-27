namespace HOAManagementCompany.Domain.Enums;

// 030 FR-010: Onboarding while being set up, Active once live, Archived when the company stops
// managing it. The 025 Inactive status is migrated to Archived.
public enum CommunityStatus
{
    Onboarding,
    Active,
    Archived
}

namespace HOAManagementCompany.Domain.Enums;

// The UI mode a user is currently in. Persisted on ApplicationUser so the
// last-used mode survives sign-out/sign-in (spec FR-022, research.md R7).
// This is UX state only and is NEVER an authorization input (FR-014).
public enum UserMode
{
    Resident,
    Board
}

using Microsoft.AspNetCore.Identity;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// Identity user: FirstName, LastName; UserProperties and RefreshTokens collections;
// Memberships (community roles — the board-side authorization source) and
// LastActiveMode (persisted Resident/Board UI mode, FR-022).
// <!-- REPOWISE:END -->

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Persisted last-used UI mode (FR-022, research.md R7). UX state only — never
    // an authorization input (FR-014).
    public UserMode LastActiveMode { get; set; } = UserMode.Resident;

    public ICollection<UserProperty> UserProperties { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<CommunityMembership> Memberships { get; set; } = [];
}

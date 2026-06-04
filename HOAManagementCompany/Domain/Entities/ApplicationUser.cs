using Microsoft.AspNetCore.Identity;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// Identity user: FirstName, LastName; UserProperties and RefreshTokens collections.
// <!-- REPOWISE:END -->

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public ICollection<UserProperty> UserProperties { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

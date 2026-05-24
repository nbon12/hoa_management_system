using Microsoft.AspNetCore.Identity;

namespace HOAManagementCompany.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public ICollection<UserProperty> UserProperties { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

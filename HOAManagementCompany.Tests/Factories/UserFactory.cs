using HOAManagementCompany.Domain.Entities;

namespace HOAManagementCompany.Tests.Factories;

public static class UserFactory
{
    public static ApplicationUser Create(
        string email = "test@nekohoa.dev",
        string firstName = "Test",
        string lastName = "User")
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            SecurityStamp = Guid.NewGuid().ToString()
        };
    }
}

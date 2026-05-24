using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Seed;

public class AuthSeeder(ApplicationDbContext db, IServiceProvider services, ILogger logger)
{
    private const string PrimaryEmail = "resident@nekohoa.dev";
    private const string SecondaryEmail = "resident2@nekohoa.dev";
    private const string Password = "Password1!";
    private const string CommunityId = "SAKURA";

    public async Task<bool> ShouldSeedAsync(CancellationToken ct = default)
        => !await db.Users.AnyAsync(u => u.Email == PrimaryEmail, ct);

    public async Task<SeedResult> SeedAsync(CancellationToken ct = default)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var primaryUser = new ApplicationUser
        {
            Email = PrimaryEmail,
            UserName = PrimaryEmail,
            FirstName = "Jane",
            LastName = "Resident",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(primaryUser, Password);

        var secondaryUser = new ApplicationUser
        {
            Email = SecondaryEmail,
            UserName = SecondaryEmail,
            FirstName = "John",
            LastName = "Resident",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(secondaryUser, Password);

        var primaryProperty = new Property
        {
            Id = Guid.NewGuid(),
            AccountNumber = "SAKURA-001",
            CommunityId = CommunityId,
            CommunityName = "Sakura Heights HOA",
            Address = "1 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A1",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        var secondaryProperty = new Property
        {
            Id = Guid.NewGuid(),
            AccountNumber = "SAKURA-002",
            CommunityId = CommunityId,
            CommunityName = "Sakura Heights HOA",
            Address = "2 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A2",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        db.Properties.AddRange(primaryProperty, secondaryProperty);
        await db.SaveChangesAsync(ct);

        db.UserProperties.AddRange(
            new UserProperty { UserId = primaryUser.Id, PropertyId = primaryProperty.Id },
            new UserProperty { UserId = secondaryUser.Id, PropertyId = secondaryProperty.Id });
        await db.SaveChangesAsync(ct);

        return new SeedResult(primaryUser.Id, secondaryUser.Id, primaryProperty.Id, secondaryProperty.Id, CommunityId);
    }
}

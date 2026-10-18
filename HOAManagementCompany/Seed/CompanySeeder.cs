using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Seed;

/// <summary>
/// 030 dev seed (quickstart.md; research R18), dev-like environments only. Idempotent, every
/// startup: makes <c>company@nekohoa.dev</c> (no home, no membership) a Company Administrator
/// through the same setup step the operator runs, and adds the Onboarding community Keystone Ridge
/// with <c>manager@nekohoa.dev</c> as its open-ended Community Manager and a few properties.
/// </summary>
public class CompanySeeder(ApplicationDbContext db, IServiceProvider services, IHostEnvironment environment, ILogger logger)
{
    private const string AdminEmail = "company@nekohoa.dev";
    private const string ManagerEmail = "manager@nekohoa.dev";
    private const string Password = "Password1!";
    private const string RidgeName = "Keystone Ridge";

    private static readonly string[] RidgeAddresses =
        ["2 Ridgeview Ct", "4 Ridgeview Ct", "6 Ridgeview Ct", "8 Ridgeview Ct", "10 Ridgeview Ct", "12 Ridgeview Ct"];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!StartupOptions.IsDevLike(environment))
            return;

        var admin = await EnsureUserAsync(AdminEmail, "Casey", "Company", ct);
        if (!await db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == admin.Id && g.RevokedAt == null, ct))
        {
            var result = await services.GetRequiredService<CompanyAdministratorService>().SetupAsync(AdminEmail, false, null, ct);
            logger.LogInformation("CompanySeeder setup step: {Output}", result.Output);
        }

        await EnsureKeystoneRidgeAsync(ct);
    }

    private async Task EnsureKeystoneRidgeAsync(CancellationToken ct)
    {
        var companyId = await DeploymentCompany.IdAsync(db, ct);
        var manager = await EnsureUserAsync(ManagerEmail, "Morgan", "Manager", ct);

        var ridge = await db.Communities.FirstOrDefaultAsync(
            c => c.ManagementCompanyId == companyId && c.NormalizedName == "keystone ridge", ct);
        if (ridge is null)
        {
            ridge = new Community
            {
                Id = Guid.NewGuid(),
                ManagementCompanyId = companyId,
                CommunityName = RidgeName,
                LegalName = "Keystone Ridge HOA, Inc.",
                County = "Durham County, NC",
                FormationDate = new DateOnly(2011, 6, 2),
                ManagementStartDate = new DateOnly(2026, 12, 1),
                Status = CommunityStatus.Onboarding,
                CreatedByUserId = manager.Id,
            };
            db.Communities.Add(ridge);
            var defaults = await db.CompanyDefaultSettings.FirstOrDefaultAsync(d => d.CompanyId == companyId, ct);
            db.CommunityArcSettings.Add(defaults?.CopyTo(ridge.Id) ?? CommunityArcSettings.Defaults(ridge.Id));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded the Onboarding community {CommunityId}.", ridge.Id);
        }

        if (!await db.CommunityMemberships.AnyAsync(m => m.CommunityId == ridge.Id && m.UserId == manager.Id
                                                         && m.Role == CommunityRole.CommunityManager, ct))
            db.CommunityMemberships.Add(new CommunityMembership
            {
                UserId = manager.Id,
                CommunityId = ridge.Id,
                Role = CommunityRole.CommunityManager,
                Status = MembershipStatus.Active,
                StartDate = new DateOnly(2026, 10, 9),
            });

        for (var i = 0; i < RidgeAddresses.Length; i++)
        {
            var account = $"KRIDGE-{i + 1:D3}";
            if (await db.Properties.AnyAsync(p => p.AccountNumber == account, ct))
                continue;
            db.Properties.Add(new Property
            {
                Id = Guid.NewGuid(), AccountNumber = account, CommunityId = ridge.Id, Address = RidgeAddresses[i],
                City = "Durham", State = "NC", Zip = "27713", Lot = (i + 1).ToString(), Section = "1",
                FiscalYear = 2026, YearBuilt = 2012, Status = "active", MonthlyAssessment = 95m, AnnualAssessment = 1140m,
                AssessmentDueDay = 1, LateFeeAmount = 25m, LateFeeGraceDays = 15, FinanceChargeRate = 0.015m,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, string first, string last, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            Email = email, UserName = email, FirstName = first, LastName = last,
            EmailConfirmed = true, LockoutEnabled = true,
        };
        var result = await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Could not create seed user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        return user;
    }
}

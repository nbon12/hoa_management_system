using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Seed;

public class SeederTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    // 020-D regression (fresh-DB bring-up): 017-A's AuthSeeder pre-creates SAKURA-003, which
    // PropertySeeder's neighbor block also defines — re-running PropertySeeder against a database
    // where those accounts exist must skip them instead of violating IX_Properties_AccountNumber.
    [Fact]
    public async Task PropertySeeder_SkipsAccountNumbersThatAlreadyExist()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<HOAManagementCompany.Infrastructure.Persistence.ApplicationDbContext>();

        var primary = await db.Properties.FirstAsync(p => p.AccountNumber == "SAKURA-001");
        var secondary = await db.Properties.FirstAsync(p => p.AccountNumber == "SAKURA-002");
        var before = await db.Properties.CountAsync();

        var seeder = new HOAManagementCompany.Seed.PropertySeeder(
            db,
            new HOAManagementCompany.Seed.SeedResult("u1", "u2", primary.Id, secondary.Id, primary.CommunityId),
            scope.ServiceProvider.GetRequiredService<
                Microsoft.Extensions.Logging.ILogger<HOAManagementCompany.Seed.DatabaseSeeder>>());

        // SAKURA-003 already exists (AuthSeeder's unclaimed property) — must be skipped, not
        // duplicated; SAKURA-777 is new and must be created.
        await seeder.SeedNeighborsAsync(
        [
            (Acct: "SAKURA-003", Addr: "3 Sakura Drive", City: "San Jose", First: "Maria", Last: "Santos",
             Email: "maria.santos@example.com", Phone: "408-555-0303",
             ShareName: true, ShareEmail: true, SharePhone: true, ShareAddress: true),
            (Acct: "SAKURA-777", Addr: "777 Sakura Drive", City: "San Jose", First: "Test", Last: "Only",
             Email: "t777@example.com", Phone: "408-555-0777",
             ShareName: true, ShareEmail: false, SharePhone: false, ShareAddress: true),
        ]);

        Assert.Equal(before + 1, await db.Properties.CountAsync());
        Assert.Equal(1, await db.Properties.CountAsync(p => p.AccountNumber == "SAKURA-003"));
        Assert.Equal(1, await db.Properties.CountAsync(p => p.AccountNumber == "SAKURA-777"));
    }

    // 025 P0-1 invariant: AuthSeeder.EnsureBoardUserAsync homes the board user on an
    // ALREADY-CLAIMED property and must never hold a link on SAKURA-003 — the property
    // E2EClaimCodeEndpoint hands out for the registration e2e. AuthService.RegisterAsync
    // refuses a property that already has a UserProperty link, so a board-user link there
    // breaks registration with 422 REGISTRATION_FAILED.
    [Fact]
    public async Task EnsureBoardUser_LeavesTheE2eRegistrationPropertyUnclaimed()
    {
        const string boardEmail = "board@nekohoa.dev";
        const string seededCommunityName = "Sakura Heights HOA";
        const string e2eAccountNumber = "SAKURA-003";

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // ── Arrange: give the seeder the shape a dev/PR database has. ──────────────
        // It finds its community by CommunityName; the shared test fixture seeds that
        // community with a LegalName only, so ensure the handle exists (otherwise the
        // seeder bails out early and the assertions below would be vacuous).
        var community = await db.Communities
            .FirstOrDefaultAsync(c => c.CommunityName == seededCommunityName);
        if (community is null)
        {
            // Fully qualified: HOAManagementCompany.Tests.Integration.Community is also a namespace.
            community = new HOAManagementCompany.Domain.Entities.Community
            {
                Id = Guid.NewGuid(),
                CommunityName = seededCommunityName,
                LegalName = seededCommunityName,
                Status = CommunityStatus.Active,
            };
            db.Communities.Add(community);
            await db.SaveChangesAsync();
        }

        // The registration property must exist, or "zero links" would be trivially true.
        var regProperty = await db.Properties.FirstOrDefaultAsync(p => p.AccountNumber == e2eAccountNumber);
        if (regProperty is null)
        {
            regProperty = NewProperty(e2eAccountNumber, community.Id);
            db.Properties.Add(regProperty);
            await db.SaveChangesAsync();
        }

        // Restore the precondition this invariant is about — SAKURA-003 unclaimed. The
        // verified-registration e2e test shares this database and legitimately claims it,
        // and test classes have no guaranteed order, so any link left by that test is
        // cleared here; what follows is then strictly about the seeder.
        var priorLinks = await db.UserProperties
            .Where(up => up.PropertyId == regProperty.Id).ToListAsync();
        if (priorLinks.Count > 0)
        {
            db.UserProperties.RemoveRange(priorLinks);
            await db.SaveChangesAsync();
        }
        Assert.Equal(0, await db.UserProperties.CountAsync(up => up.PropertyId == regProperty.Id));

        // A claimed co-residence property in that community, so the seeder has somewhere
        // safe to home the board user (without one it skips and proves nothing).
        var safeProperty = NewProperty($"SEED-SAFE-{Guid.NewGuid():N}"[..24], community.Id);
        var coResident = NewUser($"seed-test-{Guid.NewGuid():N}");
        db.Properties.Add(safeProperty);
        db.Users.Add(coResident);
        await db.SaveChangesAsync();
        db.UserProperties.Add(new UserProperty
        {
            Id = Guid.NewGuid(), UserId = coResident.Id, PropertyId = safeProperty.Id,
        });
        await db.SaveChangesAsync();

        var seeder = new HOAManagementCompany.Seed.AuthSeeder(
            db, scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<ILogger<HOAManagementCompany.Seed.DatabaseSeeder>>());

        // ── Act 1: the fresh path — the seeder creates and homes the board user. ───
        await seeder.EnsureBoardUserAsync();

        var boardUser = await db.Users.FirstOrDefaultAsync(u => u.Email == boardEmail);
        Assert.NotNull(boardUser);
        await AssertRegistrationPropertyStaysUnclaimedAsync(db, boardUser!.Id, regProperty.Id);

        // ── Act 2: the regression itself — an earlier build linked the board user to
        // SAKURA-003. Re-running the seeder must self-heal that link, not preserve it. ──
        db.UserProperties.Add(new UserProperty
        {
            Id = Guid.NewGuid(), UserId = boardUser.Id, PropertyId = regProperty.Id,
        });
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.UserProperties.CountAsync(up => up.PropertyId == regProperty.Id));

        await seeder.EnsureBoardUserAsync();

        await AssertRegistrationPropertyStaysUnclaimedAsync(db, boardUser.Id, regProperty.Id);
    }

    private static async Task AssertRegistrationPropertyStaysUnclaimedAsync(
        ApplicationDbContext db, string boardUserId, Guid regPropertyId)
    {
        // Zero links on SAKURA-003 — RegisterAsync only accepts an unclaimed property.
        Assert.Equal(0, await db.UserProperties.CountAsync(up => up.PropertyId == regPropertyId));

        // …and the board user is homed on some OTHER property (it is not simply unlinked).
        var boardLinks = await db.UserProperties
            .Where(up => up.UserId == boardUserId)
            .Select(up => up.PropertyId)
            .ToListAsync();
        Assert.NotEmpty(boardLinks);
        Assert.DoesNotContain(regPropertyId, boardLinks);
    }

    // Fully qualified: HOAManagementCompany.Tests.Integration.Property is also a namespace.
    private static HOAManagementCompany.Domain.Entities.Property NewProperty(
        string accountNumber, Guid communityId) => new()
    {
        Id = Guid.NewGuid(),
        AccountNumber = accountNumber,
        CommunityId = communityId,
        Address = "9 Seeder Lane",
        City = "San Jose",
        State = "CA",
        Zip = "95101",
        Lot = "S1",
        Section = "1",
        FiscalYear = 2026,
        YearBuilt = 2005,
        Status = "active",
        MonthlyAssessment = 250m,
        AnnualAssessment = 3000m,
        AssessmentDueDay = 1,
        LateFeeAmount = 50m,
        LateFeeGraceDays = 15,
        FinanceChargeRate = 0.015m,
    };

    private static ApplicationUser NewUser(string id)
    {
        var email = $"{id}@nekohoa.dev";
        var user = new ApplicationUser
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            FirstName = "Seed",
            LastName = "Coresident",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Password1!");
        return user;
    }

    [Fact]
    public async Task SeedData_ResidentCanLogin()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SeedData_DashboardReturnsNonEmptyData()
    {
        var loginRes = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var loginBody = await loginRes.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!["token"]!.ToString()!);

        var response = await Client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SeedData_LedgerHasAtLeast12Entries()
    {
        var loginRes = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var loginBody = await loginRes.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!["token"]!.ToString()!);

        var response = await Client.GetAsync("/api/v1/payments/ledger?pageSize=50");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var count = int.Parse(body!["totalCount"]!.ToString()!);
        Assert.True(count >= 12);
    }
}

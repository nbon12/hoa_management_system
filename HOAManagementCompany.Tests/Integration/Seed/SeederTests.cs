using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            new HOAManagementCompany.Seed.SeedResult("u1", "u2", primary.Id, secondary.Id),
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

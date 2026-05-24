using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Seed;

public class SeederTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
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

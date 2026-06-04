using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

public class TenantIsolationTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<string> GetTokenAsync(string email = "resident2@nekohoa.dev")
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["token"]!.ToString()!;
    }

    [Fact]
    public async Task Resident2_CannotSeeResident1Ledger()
    {
        // resident2 has a different propertyId in their JWT claim
        var token = await GetTokenAsync("resident2@nekohoa.dev");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/v1/payments/ledger");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        // resident2 should have 0 ledger entries (seeder only creates entries for resident1)
        var items = body!["totalCount"]?.ToString();
        Assert.Equal("0", items);
    }

    [Fact]
    public async Task Resident2_CannotSeeResident1Violations()
    {
        var token = await GetTokenAsync("resident2@nekohoa.dev");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/v1/community/violations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("0", body!["totalCount"]?.ToString());
    }
}

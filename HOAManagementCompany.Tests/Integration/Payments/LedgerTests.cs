using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

public class LedgerTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<string> GetTokenAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["token"]!.ToString()!;
    }

    [Fact]
    public async Task GetLedger_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/payments/ledger");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLedger_HappyPath_ReturnsPaginatedResults()
    {
        var token = await GetTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/v1/payments/ledger");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("items"));
        Assert.True(body.ContainsKey("totalCount"));
    }

    [Theory]
    [InlineData("?page=0", (int)422)]
    [InlineData("?pageSize=201", (int)422)]
    public async Task GetLedger_InvalidPagination_Returns422(string query, int expectedStatus)
    {
        var token = await GetTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/v1/payments/ledger{query}");
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Dashboard;

public class DashboardTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<string> GetTokenAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["token"]!.ToString()!;
    }

    [Fact]
    public async Task GetDashboard_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_WithValidToken_Returns200WithRequiredFields()
    {
        var token = await GetTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("currentBalance"));
        Assert.True(body.ContainsKey("openViolations"));
        Assert.True(body.ContainsKey("recentActivity"));
        Assert.True(body.ContainsKey("communityExpenses"));
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

public class RefreshSwitchTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<(string token, string refreshToken)> LoginAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return (body!["token"]!.ToString()!, body["refreshToken"]!.ToString()!);
    }

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithRotatedPair()
    {
        var (_, refreshToken) = await LoginAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("token"));
        Assert.True(body.ContainsKey("refreshToken"));
        Assert.NotEqual(refreshToken, body["refreshToken"]?.ToString());
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "invalid-token-that-does-not-exist" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("INVALID_REFRESH_TOKEN", body?["code"]?.ToString());
    }

    [Fact]
    public async Task SwitchProperty_UnlinkedProperty_Returns403()
    {
        var (token, _) = await LoginAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsJsonAsync("/api/v1/auth/switch-property",
            new { propertyId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("PROPERTY_ACCESS_DENIED", body?["code"]?.ToString());
    }
}

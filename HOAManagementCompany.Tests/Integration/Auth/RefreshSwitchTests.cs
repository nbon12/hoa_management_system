using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

public class RefreshSwitchTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    // 020-D FR-D1: the refresh token is transported only via the HttpOnly cookie.
    private async Task<(string token, string refreshCookie)> LoginAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("neko_refresh=")).Split(';')[0];
        return (body!["token"]!.ToString()!, cookie);
    }

    private static HttpRequestMessage RefreshWithCookie(string cookiePair)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", cookiePair);
        return req;
    }

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithRotatedPair()
    {
        var (_, refreshCookie) = await LoginAsync();

        var response = await Client.SendAsync(RefreshWithCookie(refreshCookie));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("token"));
        Assert.False(body.ContainsKey("refreshToken"));
        var rotated = response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("neko_refresh=")).Split(';')[0];
        Assert.NotEqual(refreshCookie, rotated);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await Client.SendAsync(RefreshWithCookie("neko_refresh=invalid-token-that-does-not-exist"));
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

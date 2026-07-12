using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

public class LoginLogoutTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokenPair()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("token"));
        // 020-D FR-D1: the refresh token travels only in the HttpOnly cookie, never the body.
        Assert.False(body.ContainsKey("refreshToken"));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("neko_refresh="));
    }

    [Theory]
    [InlineData("resident@nekohoa.dev", "WrongPassword!", HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS")]
    [InlineData("nobody@nowhere.com", "Password1!", HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS")]
    public async Task Login_InvalidCredentials_Returns401(
        string email, string password, HttpStatusCode expectedStatus, string expectedCode)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(expectedStatus, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(expectedCode, body?["code"]?.ToString());
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200WithProfile()
    {
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var token = loginBody!["token"]!.ToString();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResponse = await Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidToken_Returns204AndInvalidatesRefreshToken()
    {
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var token = loginBody!["token"]!.ToString();
        // 020-D FR-D1: refresh token arrives only as an HttpOnly cookie.
        var refreshCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("neko_refresh=")).Split(';')[0];

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResponse = await Client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        Client.DefaultRequestHeaders.Authorization = null;
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshRequest.Headers.Add("Cookie", refreshCookie);
        var refreshResponse = await Client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }
}

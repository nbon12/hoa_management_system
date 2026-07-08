using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Security;

// 020-D FR-D1 (contracts/auth-session.md): the refresh token travels only in an HttpOnly cookie —
// never in a response body — with strict one-time-use rotation preserved, an Origin allowlist
// check on /auth/refresh (CSRF defense for SameSite=None environments), and clearing on
// logout/invalid refresh. Test env config: Auth:RefreshCookie:SameSite=Lax.
public class AuthCookieTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SeedEmail = "resident2@nekohoa.dev";
    private const string SeedPassword = "Password1!";
    private const string AllowedOrigin = "https://dev.nekohoa.com"; // appsettings.Test.json Cors:AllowedOrigins

    private async Task<(HttpResponseMessage Response, string? Cookie)> LoginAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email = SeedEmail, password = SeedPassword });
        return (res, ExtractRefreshCookie(res));
    }

    private static string? ExtractRefreshCookie(HttpResponseMessage res) =>
        res.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("neko_refresh=", StringComparison.Ordinal))
            : null;

    private static string CookiePair(string setCookie) => setCookie.Split(';')[0];

    private HttpRequestMessage RefreshRequest(string? cookiePair, string? origin = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        if (cookiePair is not null) req.Headers.Add("Cookie", cookiePair);
        if (origin is not null) req.Headers.Add("Origin", origin);
        return req;
    }

    [Fact]
    public async Task Login_SetsHttpOnlyRefreshCookie_AndOmitsRefreshTokenFromBody()
    {
        var (res, cookie) = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=2592000", cookie, StringComparison.OrdinalIgnoreCase);

        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("token", out _));
        Assert.False(body.RootElement.TryGetProperty("refreshToken", out _),
            "refreshToken must not appear in the response body (FR-D1)");
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesTokenAndSetsNewCookie()
    {
        var (_, loginCookie) = await LoginAsync();
        Assert.NotNull(loginCookie);

        var first = await Client.SendAsync(RefreshRequest(CookiePair(loginCookie!)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = ExtractRefreshCookie(first);
        Assert.NotNull(rotated);
        Assert.NotEqual(CookiePair(loginCookie!), CookiePair(rotated!));

        // Strict one-time-use: replaying the pre-rotation cookie must fail.
        var replay = await Client.SendAsync(RefreshRequest(CookiePair(loginCookie!)));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var res = await Client.SendAsync(RefreshRequest(cookiePair: null));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://evilnekohoa-dev.pages.dev")] // suffix look-alike must not match
    public async Task Refresh_WithDisallowedOrigin_Returns401AndClearsCookie(string origin)
    {
        var (_, cookie) = await LoginAsync();

        var res = await Client.SendAsync(RefreshRequest(CookiePair(cookie!), origin));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var cleared = ExtractRefreshCookie(res);
        Assert.NotNull(cleared);
        Assert.Contains("max-age=0", cleared, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AllowedOrigin)]                          // exact allowlist entry
    [InlineData("https://abc123.nekohoa-dev.pages.dev")] // allowed suffix (preview origins)
    public async Task Refresh_WithAllowedOrigin_Succeeds(string origin)
    {
        var (_, cookie) = await LoginAsync();

        var res = await Client.SendAsync(RefreshRequest(CookiePair(cookie!), origin));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshTokenAndClearsCookie()
    {
        var (loginRes, cookie) = await LoginAsync();
        using var loginBody = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
        var access = loginBody.RootElement.GetProperty("token").GetString();

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var logoutRes = await Client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        var cleared = ExtractRefreshCookie(logoutRes);
        Assert.NotNull(cleared);
        Assert.Contains("max-age=0", cleared, StringComparison.OrdinalIgnoreCase);

        // Revoked server-side: the old cookie must no longer refresh.
        var replay = await Client.SendAsync(RefreshRequest(CookiePair(cookie!)));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task AuthResponses_AreNeverCacheable()
    {
        var (res, _) = await LoginAsync();
        Assert.Contains("no-store", res.Headers.CacheControl?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }
}

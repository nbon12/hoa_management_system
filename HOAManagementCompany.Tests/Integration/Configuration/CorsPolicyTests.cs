using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Configuration;

/// <summary>
/// Exercises the real CORS policy end-to-end. Guards the regression that broke the post-deploy
/// Playwright smoke gate: per-deploy Cloudflare Pages preview origins must be allowed via
/// Cors:AllowedOriginSuffixes, not just the exact Cors:AllowedOrigins list.
/// </summary>
public class CorsPolicyTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    // Cors:AllowedOrigins + Cors:AllowedOriginSuffixes are set in appsettings.Test.json.

    // Browsers send a CORS preflight before the login POST (JSON content-type), so the preflight
    // is exactly what failed for preview origins in Dev. Mirror that here.
    private HttpRequestMessage Preflight(string origin)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        req.Headers.TryAddWithoutValidation("Origin", origin);
        req.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        req.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "content-type");
        return req;
    }

    [Fact]
    public async Task Allows_per_deploy_pages_preview_origin()
    {
        var origin = "https://preview-abc123.nekohoa-dev.pages.dev";

        var resp = await Client.SendAsync(Preflight(origin));

        Assert.True(resp.Headers.Contains("Access-Control-Allow-Origin"),
            "preview origin should be allowed by the suffix policy");
        Assert.Equal(origin, resp.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Rejects_origin_not_in_list_or_suffix()
    {
        var resp = await Client.SendAsync(Preflight("https://evil.example.com"));

        Assert.False(resp.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

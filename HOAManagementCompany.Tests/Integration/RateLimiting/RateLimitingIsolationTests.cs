using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.RateLimiting;

/// <summary>
/// End-to-end rate-limit isolation against the real <c>auth</c> policy (014 US1 / contract
/// rate-limiting-behavior.md). Boots the host with a low, env-configured per-client limit and a
/// trusted edge secret, then drives the live <c>/auth/login</c> endpoint. Rate limiting runs before
/// the endpoint, so invalid-credential logins (401) still consume permits — no seed user required.
/// </summary>
public class RateLimitingIsolationTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const string EdgeHeader = "X-Edge-Auth";
    private const string EdgeSecret = "edge-secret-test";
    private const int PerClientLimit = 3;

    // Low, deterministic per-client limit + a configured trusted edge so the resolver can attribute
    // requests by CF-Connecting-IP (RL-6: thresholds honor configuration with no code change).
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new Dictionary<string, string?>
    {
        ["RateLimiting:AuthPermitsPerMinute"] = PerClientLimit.ToString(),
        ["RateLimiting:UnknownPermitsPerMinute"] = PerClientLimit.ToString(),
        ["RateLimiting:TrustedEdge:SecretHeaderName"] = EdgeHeader,
        ["RateLimiting:TrustedEdge:SecretHeaderValue"] = EdgeSecret,
    };

    private async Task<HttpStatusCode> LoginAsync(string? edgeSecret, string? clientIp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "nobody@nekohoa.dev", password = "WrongPassword1!" }),
        };
        if (edgeSecret is not null) request.Headers.TryAddWithoutValidation(EdgeHeader, edgeSecret);
        if (clientIp is not null) request.Headers.TryAddWithoutValidation("CF-Connecting-IP", clientIp);

        var response = await Client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task OneClientExhaustingItsQuota_DoesNotThrottleAnotherClient() // RL-1 / SC-001 / SC-002
    {
        // Client A (verified edge) spends its entire per-client window.
        for (var i = 0; i < PerClientLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(EdgeSecret, "203.0.113.10"));

        // A's next request is rejected — its own bucket is empty.
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginAsync(EdgeSecret, "203.0.113.10"));

        // Client B (different true IP) is unaffected by A's traffic.
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(EdgeSecret, "198.51.100.20"));
    }

    [Fact]
    public async Task ForgedClientIp_FromUntrustedSource_CannotEvadeTheLimit() // RL-2 / SC-003
    {
        // No edge secret → every request is un-attributable and shares the "unknown" bucket,
        // regardless of the (forged) CF-Connecting-IP the client rotates on each request.
        for (var i = 0; i < PerClientLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(edgeSecret: null, clientIp: $"203.0.113.{i + 1}"));

        // A fresh forged IP cannot mint a new bucket — the shared "unknown" window is exhausted.
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginAsync(edgeSecret: null, clientIp: "203.0.113.250"));
    }
}

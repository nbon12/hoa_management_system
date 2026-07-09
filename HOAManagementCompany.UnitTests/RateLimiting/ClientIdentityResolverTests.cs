using System.Security.Claims;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HOAManagementCompany.UnitTests.RateLimiting;

/// <summary>
/// Unit coverage for the rate-limit partition-key resolver (014 US1). Verifies the security-critical
/// behavior that a client-supplied <c>CF-Connecting-IP</c> is only trusted when the request carries the
/// configured edge secret (forged headers cannot evade or redirect limits — FR-002/SC-003), and that
/// the payments key is the authenticated subject claim, never the shared proxy address (FR-001/FR-003).
/// </summary>
public class ClientIdentityResolverTests
{
    private static readonly TrustedEdgeOptions ConfiguredEdge = new()
    {
        SecretHeaderName = "X-Edge-Auth",
        SecretHeaderValue = "edge-secret",
    };

    private static HttpContext ContextWith(params (string name, string value)[] headers)
    {
        var ctx = new DefaultHttpContext();
        foreach (var (name, value) in headers)
            ctx.Request.Headers[name] = value;
        return ctx;
    }

    // ── auth partition: trusted edge → true client IP (RL-3) ───────────────────────────────

    [Fact]
    public void Auth_TrustsCfConnectingIp_WhenEdgeSecretMatches()
    {
        var ctx = ContextWith(("X-Edge-Auth", "edge-secret"), ("CF-Connecting-IP", "203.0.113.7"));

        var key = ClientIdentityResolver.ResolveAuthPartition(ctx, ConfiguredEdge);

        Assert.Equal("203.0.113.7", key);
    }

    // ── auth partition: forged header from untrusted source is ignored (RL-2 / SC-003) ─────

    [Fact]
    public void Auth_IgnoresCfConnectingIp_WhenEdgeSecretMissing()
    {
        var ctx = ContextWith(("CF-Connecting-IP", "203.0.113.7")); // no edge secret

        var key = ClientIdentityResolver.ResolveAuthPartition(ctx, ConfiguredEdge);

        Assert.Equal(ClientIdentityResolver.UnknownPartition, key);
    }

    [Fact]
    public void Auth_IgnoresCfConnectingIp_WhenEdgeSecretMismatches()
    {
        var ctx = ContextWith(("X-Edge-Auth", "WRONG"), ("CF-Connecting-IP", "203.0.113.7"));

        var key = ClientIdentityResolver.ResolveAuthPartition(ctx, ConfiguredEdge);

        Assert.Equal(ClientIdentityResolver.UnknownPartition, key);
    }

    [Fact]
    public void Auth_IsUnknown_WhenEdgeTrustedButClientIpAbsent()
    {
        var ctx = ContextWith(("X-Edge-Auth", "edge-secret")); // verified edge, but no CF-Connecting-IP

        var key = ClientIdentityResolver.ResolveAuthPartition(ctx, ConfiguredEdge);

        Assert.Equal(ClientIdentityResolver.UnknownPartition, key);
    }

    [Fact]
    public void Auth_IsUnknown_WhenEdgeNotConfigured_EvenWithCfHeader()
    {
        // No trusted edge configured (e.g. local Development): a CF-Connecting-IP cannot be trusted.
        var ctx = ContextWith(("CF-Connecting-IP", "203.0.113.7"));

        var key = ClientIdentityResolver.ResolveAuthPartition(ctx, new TrustedEdgeOptions());

        Assert.Equal(ClientIdentityResolver.UnknownPartition, key);
    }

    // ── payments partition: authenticated subject claim (RL-5) ─────────────────────────────

    [Theory]
    [InlineData(true)]   // ClaimTypes.NameIdentifier
    [InlineData(false)]  // "sub" fallback
    public void Payments_PartitionsBySubjectClaim(bool useNameIdentifier)
    {
        var claimType = useNameIdentifier ? ClaimTypes.NameIdentifier : "sub";
        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(claimType, "user-123") }, "test")),
        };

        var key = ClientIdentityResolver.ResolvePaymentsPartition(ctx);

        Assert.Equal("user-123", key);
    }

    [Fact]
    public void Payments_IsUnknown_WhenUnauthenticated()
    {
        var ctx = new DefaultHttpContext(); // no User claims

        var key = ClientIdentityResolver.ResolvePaymentsPartition(ctx);

        Assert.Equal(ClientIdentityResolver.UnknownPartition, key);
    }

    [Fact]
    public void Payments_DistinctUsersBehindSameIp_GetDistinctKeys()
    {
        var userA = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-A") }, "test")),
        };
        var userB = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-B") }, "test")),
        };

        Assert.NotEqual(
            ClientIdentityResolver.ResolvePaymentsPartition(userA),
            ClientIdentityResolver.ResolvePaymentsPartition(userB));
    }
}

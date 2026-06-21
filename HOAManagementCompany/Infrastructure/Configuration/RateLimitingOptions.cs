// <!-- REPOWISE:START domain=rate-limiting -->
// Strongly-typed rate-limit configuration bound from the "RateLimiting" section. Drives the
// per-client `auth` (by resolved client IP) and `payments` (by authenticated user) fixed-window
// limiters in Program.cs, plus the shared `"unknown"` fallback bucket. Thresholds are env-tunable
// without code changes (014 FR-004); the trusted-edge settings let the app trust the Cloudflare
// `CF-Connecting-IP` header only on requests that carry the edge-injected shared secret (FR-002).
// <!-- REPOWISE:END -->

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Per-client rate-limit thresholds and trusted-edge identity resolution settings (014-post-deploy-hardening).
/// Bound from the <c>"RateLimiting"</c> configuration section and validated at startup.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Per-client-IP permit count per minute for the <c>auth</c> policy (login + refresh).</summary>
    public int AuthPermitsPerMinute { get; set; } = 20;

    /// <summary>Per-authenticated-user permit count per minute for the <c>payments</c> policy.</summary>
    public int PaymentsPermitsPerMinute { get; set; } = 20;

    /// <summary>Permit count per minute for the shared <c>"unknown"</c> partition (un-attributable requests).</summary>
    public int UnknownPermitsPerMinute { get; set; } = 30;

    /// <summary>Trusted-edge verification used to safely resolve the real client IP.</summary>
    public TrustedEdgeOptions TrustedEdge { get; set; } = new();
}

/// <summary>
/// Identifies the trusted Cloudflare edge. When a request carries <see cref="SecretHeaderName"/>
/// equal to <see cref="SecretHeaderValue"/> (injected by a Cloudflare Transform Rule at the edge),
/// the <c>CF-Connecting-IP</c> header is trusted as the true client IP. Otherwise the request is
/// treated as un-attributable. Both values are unset for local <c>Development</c>/<c>Test</c>, where
/// no edge exists; the secret is supplied from a managed secret store in deployed environments and
/// is never committed.
/// </summary>
public sealed class TrustedEdgeOptions
{
    /// <summary>Header name the edge injects to prove the request transited Cloudflare (e.g. <c>X-Edge-Auth</c>).</summary>
    public string? SecretHeaderName { get; set; }

    /// <summary>Expected secret value for <see cref="SecretHeaderName"/>; sourced from secret config.</summary>
    public string? SecretHeaderValue { get; set; }

    /// <summary>True when both the header name and value are configured.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretHeaderName) && !string.IsNullOrWhiteSpace(SecretHeaderValue);
}

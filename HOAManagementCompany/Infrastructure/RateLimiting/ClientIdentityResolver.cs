// <!-- REPOWISE:START domain=rate-limiting -->
// Resolves the rate-limit partition key for a request (014-post-deploy-hardening).
//   * auth policy     → the true client IP, taken from Cloudflare's `CF-Connecting-IP` header but
//                       ONLY when the request carries the configured edge-secret header (proving it
//                       transited the trusted Cloudflare edge). A client-supplied `CF-Connecting-IP`
//                       on a request that did NOT come through the edge is ignored — it cannot forge
//                       or redirect another client's limit (FR-002). Unresolved → "unknown".
//   * payments policy → the authenticated user's subject claim (NameIdentifier, falling back to
//                       "sub"), matching MeEndpoint/LogoutEndpoint/TraceEnrichmentMiddleware. So two
//                       users behind one NAT IP never share a payment quota (FR-001/FR-003).
// Un-attributable requests in either policy collapse to a single shared "unknown" partition with its
// own strict quota — never a per-proxy-IP or unbounded bucket — so a flood of header-less requests
// throttles only itself (fail-safe).
// <!-- REPOWISE:END -->

using System.Security.Claims;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;

namespace HOAManagementCompany.Infrastructure.RateLimiting;

/// <summary>Shared partition key for all un-attributable requests.</summary>
public static class ClientIdentityResolver
{
    public const string UnknownPartition = "unknown";

    private const string CloudflareClientIpHeader = "CF-Connecting-IP";

    /// <summary>
    /// Resolves the <c>auth</c> partition key: the trusted client IP when the request came through
    /// the configured edge, otherwise <see cref="UnknownPartition"/>. Never returns the proxy/
    /// connection address (which would be shared across all clients).
    /// </summary>
    public static string ResolveAuthPartition(HttpContext context, TrustedEdgeOptions edge)
    {
        if (!IsFromTrustedEdge(context, edge))
            return UnknownPartition;

        var clientIp = context.Request.Headers[CloudflareClientIpHeader].ToString();
        return string.IsNullOrWhiteSpace(clientIp) ? UnknownPartition : clientIp;
    }

    /// <summary>
    /// Resolves the <c>payments</c> partition key: the authenticated user's subject claim, otherwise
    /// <see cref="UnknownPartition"/>. Requires the limiter to run after authentication.
    /// </summary>
    public static string ResolvePaymentsPartition(HttpContext context)
    {
        var subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? context.User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(subject) ? UnknownPartition : subject;
    }

    // A request is from the trusted edge only when the edge secret is configured AND the request
    // presents the matching header value. Constant-time comparison is unnecessary here (the secret
    // gates header trust, not authentication), but an ordinal match avoids culture surprises.
    private static bool IsFromTrustedEdge(HttpContext context, TrustedEdgeOptions edge)
    {
        if (!edge.IsConfigured)
            return false;

        var presented = context.Request.Headers[edge.SecretHeaderName!].ToString();
        return !string.IsNullOrEmpty(presented)
               && string.Equals(presented, edge.SecretHeaderValue, StringComparison.Ordinal);
    }
}

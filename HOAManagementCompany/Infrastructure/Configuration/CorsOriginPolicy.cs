using System;
using System.Collections.Generic;
using System.Linq;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Decides whether a browser Origin is allowed by CORS. Beyond the exact allow-list, it permits
/// hosts under configured suffixes — used in Dev so per-deploy Cloudflare Pages preview origins
/// (https://&lt;hash&gt;.nekohoa-dev.pages.dev), which can't be enumerated ahead of time, can reach
/// the API. Suffix matching is host-only and requires a leading dot, so "nekohoa-dev.pages.dev"
/// matches "x.nekohoa-dev.pages.dev" but not "evilnekohoa-dev.pages.dev".
/// </summary>
public static class CorsOriginPolicy
{
    public static bool IsAllowed(string? origin, IReadOnlyCollection<string> exactOrigins, IReadOnlyCollection<string> hostSuffixes)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (exactOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            return true;

        if (hostSuffixes.Count == 0 || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        return hostSuffixes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.StartsWith('.') ? s : "." + s)
            .Any(s => ("." + uri.Host).EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}

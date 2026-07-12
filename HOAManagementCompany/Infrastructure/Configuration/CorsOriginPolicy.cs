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
/// <summary>
/// The resolved origin allow-list the CORS middleware actually uses — including the local
/// Development fallback to localhost:4200 applied when configuration is unset. Registered as a
/// singleton so other origin checks (e.g. the refresh endpoint's CSRF defense, 020-D) share the
/// exact same policy instead of re-deriving it from raw configuration and diverging.
/// </summary>
public sealed record CorsOriginSettings(IReadOnlyCollection<string> Origins, IReadOnlyCollection<string> Suffixes)
{
    public bool IsAllowed(string? origin) => CorsOriginPolicy.IsAllowed(origin, Origins, Suffixes);
}

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

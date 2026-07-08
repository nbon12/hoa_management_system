namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Refresh-token cookie transport settings (020-D FR-D1). Bound from <c>"Auth:RefreshCookie"</c>.
/// The cookie is always HttpOnly + Secure with <c>Path=/api/v1/auth</c> and a Max-Age equal to the
/// refresh-token lifetime; only <see cref="SameSite"/> varies by environment, because the frontend
/// and API are same-site in Production (nekohoa.com) but cross-site in Dev/PR environments
/// (pages.dev preview origins → run.app / api-dev), where Strict/Lax cookies would never be sent
/// (see specs/020-security-hardening-subspec-d/contracts/auth-session.md).
/// </summary>
public sealed class RefreshCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";

    public const string CookieName = "neko_refresh";
    public const string CookiePath = "/api/v1/auth";

    /// <summary>"Strict" (Production), "None" (Dev / PR envs), or "Lax" (local Development/Test).</summary>
    public string SameSite { get; set; } = "";
}

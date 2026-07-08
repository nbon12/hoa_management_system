using HOAManagementCompany.Infrastructure.Configuration;

namespace HOAManagementCompany.Features.Auth;

// 020-D FR-D1 (contracts/auth-session.md): the refresh token travels only in this HttpOnly cookie.
// SameSite is config-driven because the frontend and API are same-site in Production but
// cross-site in Dev/PR environments (pages.dev preview origins), where Strict/Lax would never be
// sent. Auth responses are marked no-store — a cached Set-Cookie would be a session leak.
public static class RefreshCookie
{
    public static void Append(HttpContext ctx, string refreshToken, RefreshCookieOptions options, int lifetimeDays)
        => Write(ctx, refreshToken, options, TimeSpan.FromDays(lifetimeDays));

    public static void Clear(HttpContext ctx, RefreshCookieOptions options)
        => Write(ctx, "", options, TimeSpan.Zero);

    private static void Write(HttpContext ctx, string value, RefreshCookieOptions options, TimeSpan maxAge)
    {
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Cookies.Append(RefreshCookieOptions.CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = ParseSameSite(options.SameSite),
            Path = RefreshCookieOptions.CookiePath,
            MaxAge = maxAge
        });
    }

    private static SameSiteMode ParseSameSite(string value) => value switch
    {
        "Strict" => SameSiteMode.Strict,
        "None" => SameSiteMode.None,
        _ => SameSiteMode.Lax
    };
}

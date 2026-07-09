using FastEndpoints;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Auth;

// <!-- REPOWISE:START domain=auth-session -->
// 020-D FR-D1 (contracts/auth-session.md): the refresh token is read exclusively from the
// HttpOnly cookie — the request has no body. Browser-sent Origin (fallback Referer) must match
// the CORS allowlist as CSRF defense-in-depth for the SameSite=None environments; requests
// carrying neither header (non-browser clients: tests, tooling) pass — they cannot ride a
// victim's cookie jar. Failures are generic and clear the cookie. Rotation stays strict
// one-time-use; cross-tab refresh races are prevented client-side (Web Locks), not by a
// server-side grace window.
// <!-- REPOWISE:END -->
public class RefreshEndpoint(
    AuthService authService,
    IOptions<RefreshCookieOptions> cookieOptions,
    IConfiguration config) : EndpointWithoutRequest<AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Description(x => x.WithName("RefreshToken").WithTags("Auth").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            if (!IsBrowserOriginAllowed())
                throw new DomainException("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.", 401);

            var rawToken = HttpContext.Request.Cookies[RefreshCookieOptions.CookieName];
            if (string.IsNullOrEmpty(rawToken))
                throw new DomainException("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.", 401);

            var result = await authService.RefreshAsync(rawToken, ct);
            RefreshCookie.Append(HttpContext, result.RefreshToken, cookieOptions.Value,
                config.GetValue("Jwt:RefreshTokenExpiryDays", 30));
            await SendOkAsync(result.Response, ct);
        }
        catch (DomainException ex)
        {
            RefreshCookie.Clear(HttpContext, cookieOptions.Value);
            HttpContext.Response.StatusCode = ex.StatusCode;
            await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }

    private bool IsBrowserOriginAllowed()
    {
        var origin = HttpContext.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrEmpty(origin))
        {
            var referer = HttpContext.Request.Headers.Referer.FirstOrDefault();
            if (string.IsNullOrEmpty(referer))
                return true; // no browser-context headers — not a cookie-riding CSRF vector
            origin = Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                ? uri.GetLeftPart(UriPartial.Authority)
                : referer;
        }

        var exact = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var suffixes = config.GetSection("Cors:AllowedOriginSuffixes").Get<string[]>() ?? [];
        return CorsOriginPolicy.IsAllowed(origin, exact, suffixes);
    }
}

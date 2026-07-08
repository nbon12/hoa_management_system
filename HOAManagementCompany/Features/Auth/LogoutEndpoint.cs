using FastEndpoints;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Auth;

public class LogoutEndpoint(
    AuthService authService,
    IOptions<RefreshCookieOptions> cookieOptions) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/logout");
        Description(x => x.WithName("Logout").WithTags("Auth"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;

        await authService.LogoutAsync(userId, ct);
        // 020-D FR-D1: clear the HttpOnly refresh cookie alongside server-side revocation.
        RefreshCookie.Clear(HttpContext, cookieOptions.Value);
        await SendNoContentAsync(ct);
    }
}

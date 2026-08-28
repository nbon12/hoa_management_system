using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Board;

// POST /api/v1/auth/board-mode (User Story 1). One login for all personas — this
// switches the caller's active UI mode in place; there is no separate portal (FR-018).
// The server independently verifies an active non-resident membership before allowing
// Board mode (FR-014/FR-020) and persists the choice (FR-022). Mirrors
// SwitchPropertyEndpoint's cookie-rotation pattern.
public class BoardModeEndpoint(
    AuthService authService,
    IOptions<RefreshCookieOptions> cookieOptions,
    IConfiguration config) : Endpoint<BoardModeRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/board-mode");
        Description(x => x.WithName("BoardMode").WithTags("Auth"));
    }

    public override async Task HandleAsync(BoardModeRequest req, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;

        if (!Enum.TryParse<UserMode>(req.Mode, ignoreCase: true, out var mode))
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(
                new { code = "VALIDATION_ERROR", message = "Unknown mode." }, ct);
            return;
        }

        try
        {
            var result = await authService.SwitchModeAsync(userId, mode, ct);
            RefreshCookie.Append(HttpContext, result.RefreshToken, cookieOptions.Value,
                config.GetValue("Jwt:RefreshTokenExpiryDays", 30));
            await SendOkAsync(result.Response, ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
            await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

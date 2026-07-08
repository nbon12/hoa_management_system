using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Auth;

public class SwitchPropertyEndpoint(
    AuthService authService,
    IOptions<RefreshCookieOptions> cookieOptions,
    IConfiguration config) : Endpoint<SwitchPropertyRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/switch-property");
        Description(x => x.WithName("SwitchProperty").WithTags("Auth"));
    }

    public override async Task HandleAsync(SwitchPropertyRequest req, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;

        try
        {
            var result = await authService.SwitchPropertyAsync(userId, req.PropertyId, ct);
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

public class SwitchPropertyValidator : Validator<SwitchPropertyRequest>
{
    public SwitchPropertyValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
    }
}

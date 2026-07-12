using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Auth;

public class LoginEndpoint(
    AuthService authService,
    IOptions<RefreshCookieOptions> cookieOptions,
    IConfiguration config) : Endpoint<LoginRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Description(x => x.WithName("Login").WithTags("Auth").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        try
        {
            var result = await authService.LoginAsync(req, ct);
            // 020-D FR-D1: refresh token travels only in the HttpOnly cookie, never the body.
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

public class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

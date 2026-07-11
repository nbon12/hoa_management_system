using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth.Models;

namespace HOAManagementCompany.Features.Auth;

public class RegisterEndpoint(AuthService authService) : Endpoint<RegisterRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        // 016-A FR-A2: throttle registration.
        Description(x => x.WithName("Register").WithTags("Auth").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        try
        {
            var response = await authService.RegisterAsync(req, ct);
            await SendAsync(response, 201, ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

public class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.VerificationToken).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ClaimCode).NotEmpty();
    }
}

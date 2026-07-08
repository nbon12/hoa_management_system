using HOAManagementCompany.Domain;
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
        Description(x => x.WithName("Register").WithTags("Auth"));
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
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AccountNumber).NotEmpty();
    }
}

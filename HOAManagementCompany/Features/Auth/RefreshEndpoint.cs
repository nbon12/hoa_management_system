using HOAManagementCompany.Domain;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth.Models;

namespace HOAManagementCompany.Features.Auth;

public class RefreshEndpoint(AuthService authService) : Endpoint<RefreshRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Description(x => x.WithName("RefreshToken").WithTags("Auth").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
    {
        try
        {
            var response = await authService.RefreshAsync(req.RefreshToken, ct);
            await SendOkAsync(response, ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

public class RefreshValidator : Validator<RefreshRequest>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

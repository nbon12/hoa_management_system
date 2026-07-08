using HOAManagementCompany.Domain;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth.Models;

namespace HOAManagementCompany.Features.Auth;

public class SwitchPropertyEndpoint(AuthService authService) : Endpoint<SwitchPropertyRequest, AuthResponse>
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
            var response = await authService.SwitchPropertyAsync(userId, req.PropertyId, ct);
            await SendOkAsync(response, ct);
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

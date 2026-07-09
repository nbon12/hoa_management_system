using HOAManagementCompany.Features.Common;
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
        var userId = User.GetUserId();

        var response = await authService.SwitchPropertyAsync(userId, req.PropertyId, ct);
        await SendOkAsync(response, ct);
    }
}

public class SwitchPropertyValidator : Validator<SwitchPropertyRequest>
{
    public SwitchPropertyValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
    }
}

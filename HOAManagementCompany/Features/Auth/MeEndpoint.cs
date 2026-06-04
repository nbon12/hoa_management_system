using FastEndpoints;
using HOAManagementCompany.Features.Auth.Models;

namespace HOAManagementCompany.Features.Auth;

public class MeEndpoint(AuthService authService) : EndpointWithoutRequest<CurrentUserDto>
{
    public override void Configure()
    {
        Get("/auth/me");
        Description(x => x.WithName("GetMe").WithTags("Auth"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;

        var user = await authService.GetCurrentUserAsync(userId, ct);
        await SendOkAsync(user, ct);
    }
}

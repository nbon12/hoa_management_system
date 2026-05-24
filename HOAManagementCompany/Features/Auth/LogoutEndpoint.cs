using FastEndpoints;
using HOAManagementCompany.Features.Auth.Models;

namespace HOAManagementCompany.Features.Auth;

public class LogoutEndpoint(AuthService authService) : EndpointWithoutRequest
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
        await SendNoContentAsync(ct);
    }
}

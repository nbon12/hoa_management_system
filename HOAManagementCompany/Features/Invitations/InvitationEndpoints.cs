using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Invitations;

// 030 FR-022, FR-022a (contracts/invitations-and-auth.md § Invitation links): the invitation link
// endpoints. They are authorized by the link token, never by the resolver, use the per-client
// `auth` rate limit (FR-041b) and are never cached. The token travels only in request bodies.

public sealed record InvitationTokenRequest(string Token);

public sealed record InvitationRegisterRequest(string Token, string FirstName, string LastName, string Password);

public sealed record InvitationAcceptResponse(bool Accepted, bool AlreadyHeld, string Landing);

/// <summary>POST /invitations/preview — what a valid, unexpired link offers.</summary>
public class InvitationPreviewEndpoint(InvitationService invitations) : Endpoint<InvitationTokenRequest, InvitationPreview>
{
    public override void Configure()
    {
        Post("/invitations/preview");
        AllowAnonymous();
        Description(x => x.WithName("InvitationPreview").WithTags("Invitations").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(InvitationTokenRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        // A caller with a valid bearer token is signed in, so the invited email is withheld (FR-022a).
        var anonymous = User.Identity?.IsAuthenticated != true;
        await SendOkAsync(await invitations.PreviewAsync(req.Token, anonymous, ct), ct);
    }
}

/// <summary>POST /invitations/register — creates the invited account from the link, accepts, and signs in.</summary>
public class InvitationRegisterEndpoint(
    InvitationService invitations,
    AuthService authService,
    IOptions<RefreshCookieOptions> cookieOptions,
    IConfiguration config) : Endpoint<InvitationRegisterRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/invitations/register");
        AllowAnonymous();
        Description(x => x.WithName("InvitationRegister").WithTags("Invitations").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(InvitationRegisterRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var user = await invitations.RegisterAsync(req.Token, req.FirstName, req.LastName, req.Password, ct);
        var result = await authService.SignInAsync(user, ct);
        RefreshCookie.Append(HttpContext, result.RefreshToken, cookieOptions.Value,
            config.GetValue("Jwt:RefreshTokenExpiryDays", 30));
        await SendAsync(result.Response, StatusCodes.Status201Created, ct);
    }
}

/// <summary>POST /invitations/accept — applies the link's offer to the signed-in caller.</summary>
public class InvitationAcceptEndpoint(InvitationService invitations, AuthService authService)
    : Endpoint<InvitationTokenRequest, InvitationAcceptResponse>
{
    public override void Configure()
    {
        Post("/invitations/accept");
        Description(x => x.WithName("InvitationAccept").WithTags("Invitations").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(InvitationTokenRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var outcome = await invitations.AcceptAsync(req.Token, userId, ct);
        var me = await authService.GetCurrentUserAsync(userId, ct);
        await SendOkAsync(new InvitationAcceptResponse(true, outcome.AlreadyHeld, me.Landing), ct);
    }
}

public class InvitationTokenValidator : Validator<InvitationTokenRequest>
{
    public InvitationTokenValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
    }
}

public class InvitationRegisterValidator : Validator<InvitationRegisterRequest>
{
    public InvitationRegisterValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).OverridePropertyName("firstName");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).OverridePropertyName("lastName");
        // The Identity rules (8+ characters, a digit, upper and lower case, a symbol) are checked on create.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200).OverridePropertyName("password");
    }
}

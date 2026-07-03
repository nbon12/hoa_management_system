using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth.Models;

namespace HOAManagementCompany.Features.Auth;

// 016-A FR-A3: prove control of an email before any registration/claim state is revealed.
public class VerifyEmailRequestEndpoint(EmailVerificationService verification)
    : Endpoint<VerifyEmailRequest, VerifyEmailRequestResponse>
{
    public override void Configure()
    {
        Post("/auth/verify-email/request");
        AllowAnonymous();
        Description(x => x.WithName("VerifyEmailRequest").WithTags("Auth").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(VerifyEmailRequest req, CancellationToken ct)
    {
        await verification.RequestAsync(req.Email, EmailVerificationService.PurposeRegistration, null, ct);
        // Uniform response regardless of account state (FR-A5).
        await SendAsync(new VerifyEmailRequestResponse("if_eligible_a_code_was_sent"), 202, ct);
    }
}

public class VerifyEmailConfirmEndpoint(EmailVerificationService verification)
    : Endpoint<VerifyEmailConfirmRequest, VerifyEmailConfirmResponse>
{
    public override void Configure()
    {
        Post("/auth/verify-email/confirm");
        AllowAnonymous();
        Description(x => x.WithName("VerifyEmailConfirm").WithTags("Auth").RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(VerifyEmailConfirmRequest req, CancellationToken ct)
    {
        var proof = await verification.ConfirmAsync(
            req.Email, req.Code, EmailVerificationService.PurposeRegistration, ct);
        if (proof is null)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(
                new { code = "INVALID_OR_EXPIRED", message = "The code is invalid or has expired." }, ct);
            return;
        }
        await SendAsync(new VerifyEmailConfirmResponse(proof), 200, ct);
    }
}

public class VerifyEmailRequestValidator : Validator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public class VerifyEmailConfirmValidator : Validator<VerifyEmailConfirmRequest>
{
    public VerifyEmailConfirmValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

using FluentValidation;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="StripeOptions"/> at startup. Stripe credentials are required in every
/// environment (presence, not authenticity — a placeholder satisfies the check; 008 FR-004).
/// </summary>
public sealed class StripeOptionsValidator : AbstractValidator<StripeOptions>
{
    public StripeOptionsValidator()
    {
        RuleFor(x => x.SecretKey).NotEmpty()
            .WithMessage("Stripe:SecretKey is required.");
        RuleFor(x => x.PublishableKey).NotEmpty()
            .WithMessage("Stripe:PublishableKey is required.");
        RuleFor(x => x.WebhookSigningSecret).NotEmpty()
            .WithMessage("Stripe:WebhookSigningSecret is required.");
        RuleFor(x => x.WebhookToleranceSeconds).GreaterThan(0L)
            .WithMessage("Stripe:WebhookToleranceSeconds must be greater than 0.");
    }
}

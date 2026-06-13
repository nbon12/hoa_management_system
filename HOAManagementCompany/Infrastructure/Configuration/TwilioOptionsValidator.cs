using FluentValidation;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="TwilioOptions"/> at startup. Twilio SMS alerting is optional: leaving
/// every field blank disables it and is valid. But once any field is set the provider is treated
/// as "in use" and must be complete enough to actually send — each missing piece is reported
/// separately so the operator sees exactly what to fix (008 FR-012). The accepted auth pairings
/// mirror <see cref="TwilioOptions.IsConfigured"/> so validation and the adapter agree.
/// </summary>
public sealed class TwilioOptionsValidator : AbstractValidator<TwilioOptions>
{
    public TwilioOptionsValidator()
    {
        // Only enforce the field-level rules when Twilio is in use; all-empty stays valid.
        When(o => !IsFullyEmpty(o), () =>
        {
            RuleFor(x => x.AccountSid).NotEmpty()
                .WithMessage("Twilio:AccountSid is required when Twilio is configured.");

            RuleFor(x => x.FromNumber).NotEmpty()
                .WithMessage("Twilio:FromNumber is required when Twilio is configured.");

            RuleFor(x => x)
                .Must(HasUsableAuth)
                .OverridePropertyName("Twilio:Auth")
                .WithMessage(
                    "Twilio requires a usable auth pairing: ApiKeySid + ApiKeySecret (API-key " +
                    "auth) or AuthToken (basic auth).");
        });
    }

    private static bool IsFullyEmpty(TwilioOptions o) =>
        string.IsNullOrWhiteSpace(o.AccountSid)
        && string.IsNullOrWhiteSpace(o.ApiKeySid)
        && string.IsNullOrWhiteSpace(o.ApiKeySecret)
        && string.IsNullOrWhiteSpace(o.FromNumber)
        && string.IsNullOrWhiteSpace(o.AuthToken);

    private static bool HasUsableAuth(TwilioOptions o) =>
        (!string.IsNullOrWhiteSpace(o.ApiKeySid) && !string.IsNullOrWhiteSpace(o.ApiKeySecret))
        || !string.IsNullOrWhiteSpace(o.AuthToken);
}

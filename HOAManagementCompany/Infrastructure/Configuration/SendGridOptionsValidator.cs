using FluentValidation;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="SendGridOptions"/> at startup. Email alerting is optional: leaving every
/// field blank disables it and is valid. Once any field is set the provider is treated as "in use"
/// and each missing/invalid piece is reported separately (008 FR-012). "In use" mirrors the inputs
/// behind <see cref="SendGridOptions.IsConfigured"/>.
/// </summary>
public sealed class SendGridOptionsValidator : AbstractValidator<SendGridOptions>
{
    public SendGridOptionsValidator()
    {
        When(o => !IsFullyEmpty(o), () =>
        {
            RuleFor(x => x.ApiKey).NotEmpty()
                .WithMessage("SendGrid:ApiKey is required when SendGrid is configured.");

            RuleFor(x => x.FromEmail).NotEmpty()
                .WithMessage("SendGrid:FromEmail is required when SendGrid is configured.");

            RuleFor(x => x.FromEmail)
                .EmailAddress()
                .When(o => !string.IsNullOrWhiteSpace(o.FromEmail))
                .WithMessage("SendGrid:FromEmail must be a valid email address.");
        });
    }

    private static bool IsFullyEmpty(SendGridOptions o) =>
        string.IsNullOrWhiteSpace(o.ApiKey)
        && string.IsNullOrWhiteSpace(o.FromEmail);
}

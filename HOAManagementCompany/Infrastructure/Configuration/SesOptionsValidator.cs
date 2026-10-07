using Amazon;
using FluentValidation;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="SesOptions"/> at startup (026 US3). Email is optional: leaving every field
/// blank disables it and is valid. Once any field is set the provider is treated as "in use" and each
/// missing/invalid piece is reported separately (008 FR-012). Messages name settings, never values.
/// </summary>
public sealed class SesOptionsValidator : AbstractValidator<SesOptions>
{
    private static readonly HashSet<string> KnownRegions =
        RegionEndpoint.EnumerableAllRegions.Select(r => r.SystemName).ToHashSet(StringComparer.Ordinal);

    public SesOptionsValidator()
    {
        When(o => !IsFullyEmpty(o), () =>
        {
            RuleFor(x => x.Region).NotEmpty()
                .WithMessage("Ses:Region is required when SES is configured.");

            RuleFor(x => x.Region)
                .Must(r => KnownRegions.Contains(r))
                .When(o => !string.IsNullOrWhiteSpace(o.Region))
                .WithMessage("Ses:Region must be a valid AWS region name (e.g. us-east-1).");

            RuleFor(x => x.FromEmail).NotEmpty()
                .WithMessage("Ses:FromEmail is required when SES is configured.");

            RuleFor(x => x.FromEmail)
                .EmailAddress()
                .When(o => !string.IsNullOrWhiteSpace(o.FromEmail))
                .WithMessage("Ses:FromEmail must be a valid email address.");

            RuleFor(x => x)
                .Must(o => string.IsNullOrWhiteSpace(o.AccessKeyId) == string.IsNullOrWhiteSpace(o.SecretAccessKey))
                .WithName("Ses")
                .WithMessage("Ses:AccessKeyId and Ses:SecretAccessKey must be set together.");
        });
    }

    private static bool IsFullyEmpty(SesOptions o) =>
        string.IsNullOrWhiteSpace(o.Region)
        && string.IsNullOrWhiteSpace(o.FromEmail)
        && string.IsNullOrWhiteSpace(o.AccessKeyId)
        && string.IsNullOrWhiteSpace(o.SecretAccessKey);
}

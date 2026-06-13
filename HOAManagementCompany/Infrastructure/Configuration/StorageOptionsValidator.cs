using System;
using FluentValidation;
using HOAManagementCompany.Infrastructure.Storage;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="StorageOptions"/> at startup. Replaces the previous null-forgiving bind
/// in Program.cs, which produced a NullReferenceException deep in DI when the Storage section was
/// missing; now a clear, field-level error is reported instead (008 FR-011).
/// </summary>
public sealed class StorageOptionsValidator : AbstractValidator<StorageOptions>
{
    public StorageOptionsValidator()
    {
        RuleFor(x => x.ServiceUrl).NotEmpty()
            .WithMessage("Storage:ServiceUrl is required.");
        RuleFor(x => x.ServiceUrl)
            .Must(BeAbsoluteUri)
            .When(x => !string.IsNullOrWhiteSpace(x.ServiceUrl))
            .WithMessage("Storage:ServiceUrl must be a valid absolute URI.");

        RuleFor(x => x.AccessKey).NotEmpty()
            .WithMessage("Storage:AccessKey is required.");
        RuleFor(x => x.SecretKey).NotEmpty()
            .WithMessage("Storage:SecretKey is required.");
        RuleFor(x => x.BucketName).NotEmpty()
            .WithMessage("Storage:BucketName is required.");

        RuleFor(x => x.PublicServiceUrl)
            .Must(BeAbsoluteUri)
            .When(x => !string.IsNullOrWhiteSpace(x.PublicServiceUrl))
            .WithMessage("Storage:PublicServiceUrl must be a valid absolute URI when set.");
    }

    private static bool BeAbsoluteUri(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out _);
}

using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="DevToolsOptions"/> at startup (constitution §8 — new configuration MUST ship
/// with its validator and be bound-and-validated with fail-fast behavior). After environment defaults
/// are applied, <see cref="DevToolsOptions.ExposeExceptionDetail"/> must be resolved (non-null); the
/// boolean toggles have no further range constraints.
/// </summary>
public sealed class DevToolsOptionsValidator : AbstractValidator<DevToolsOptions>
{
    public DevToolsOptionsValidator()
    {
        RuleFor(x => x.ExposeExceptionDetail).NotNull()
            .WithMessage("DevTools:ExposeExceptionDetail must be resolved to a concrete value at startup.");
    }
}

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

        // 015 US3 (FR-009): environment-level backstop above configuration — the test-cleanup
        // endpoint can never be enabled in Production, whatever the config flags say. Boot refuses.
        RuleFor(x => x.E2ECleanupEnabled).Equal(false)
            .When(x => x.EnvironmentName == Microsoft.Extensions.Hosting.Environments.Production)
            .WithMessage("DevTools:E2ECleanupEnabled cannot be enabled in Production — test machinery is disabled by an environment backstop (015 US3).");
    }
}

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Wraps the host's <c>ASPNETCORE_ENVIRONMENT</c> name so it can be validated at startup through the
/// same FluentValidation + ValidateOnStart pipeline as the other options groups (010-dev-env-iac,
/// constitution §8). The IaC sets <c>ASPNETCORE_ENVIRONMENT</c> on Cloud Run (FR-006/FR-030); this
/// guard rejects a mis-set value (e.g. <c>prod</c> instead of <c>Production</c>, or a lowercase
/// <c>development</c>) at boot rather than letting <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsProduction"/>
/// silently mis-classify the environment.
/// </summary>
public sealed class HostEnvironmentOptions
{
    /// <summary>The resolved host environment name (from <c>builder.Environment.EnvironmentName</c>).</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>
    /// The exact, case-sensitive set of environment names this codebase recognizes:
    /// <c>Development</c> (local), <c>Dev</c> (deployed dev), <c>Test</c>, <c>Staging</c>,
    /// <c>Production</c> — matching the existing <c>appsettings.{Development,Dev,Test}.json</c> plus
    /// the deployed Staging/Prod. Anything else (including wrong casing) is rejected.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownEnvironments = new HashSet<string>(StringComparer.Ordinal)
    {
        "Development",
        "Dev",
        "Test",
        "Staging",
        "Production",
    };
}

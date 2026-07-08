using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Controls environment-dependent startup behavior that used to be hardcoded to
/// <c>IsDevelopment()</c> (feature 009-dev-auto-deploy). Bound from the <c>"Startup"</c>
/// configuration section and resolved once at startup.
/// <para>
/// Defaults are derived from the hosting environment when the section is absent, so existing
/// local <c>Development</c> behavior (auto-migrate, seed, Swagger) is preserved with no config,
/// while a deployed <c>Dev</c> service gets the same conveniences and <c>Test</c>/<c>Staging</c>/
/// <c>Production</c> stay off. Explicit config values always win over the env-derived default —
/// except that Swagger is force-disabled in Production regardless of config (Constitution §4).
/// </para>
/// </summary>
public sealed class StartupOptions
{
    public const string SectionName = "Startup";

    /// <summary>The deployed non-production environment name (distinct from local <c>Development</c>).</summary>
    public const string DevEnvironmentName = "Dev";

    /// <summary>Apply EF Core migrations idempotently at startup before serving traffic.</summary>
    public bool ApplyMigrations { get; set; }

    /// <summary>Seed reference/synthetic data at startup (idempotent; migrates first).</summary>
    public bool SeedData { get; set; }

    /// <summary>Expose Swagger UI. Always false in Production (Constitution §4).</summary>
    public bool EnableSwagger { get; set; }

    /// <summary>True for local <c>Development</c> and the deployed <c>Dev</c> environment.</summary>
    public static bool IsDevLike(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(DevEnvironmentName);

    /// <summary>
    /// Resolve the effective options: start from env-derived defaults, bind the
    /// <c>"Startup"</c> section over them, then enforce the production Swagger invariant.
    /// </summary>
    public static StartupOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var devLike = IsDevLike(environment);
        var options = new StartupOptions
        {
            ApplyMigrations = devLike,
            SeedData = devLike,
            EnableSwagger = devLike,
        };

        configuration.GetSection(SectionName).Bind(options);

        // Hard invariant: Swagger is never enabled in Production, whatever config says.
        if (environment.IsProduction())
            options.EnableSwagger = false;

        // 015 US3 (FR-009): seeding is test machinery. It may only run in explicitly known
        // non-production environments; Production — or an ambiguous/unknown environment name,
        // which must default to "test machinery disabled" — fails fast at boot instead of
        // silently honoring the flag. This runs before any database access (unlike the
        // ValidateOnStart pipeline, which fires at host start, after startup tasks).
        string[] seedableEnvironments = ["Development", DevEnvironmentName, "Test", "Staging"];
        if (options.SeedData && !seedableEnvironments.Contains(environment.EnvironmentName, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Startup:SeedData cannot be enabled in environment '{environment.EnvironmentName}' — test machinery is disabled by an environment backstop (015 US3).");

        return options;
    }
}

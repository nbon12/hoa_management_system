using Microsoft.Extensions.Hosting;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Configuration-driven developer tooling toggles (014-post-deploy-hardening). Bound from the
/// <c>"DevTools"</c> section. These replace host-environment-name (<c>IsDevelopment()</c>) gating that
/// silently no-ops in the deployed <c>Dev</c> environment: defaults derive from
/// <see cref="StartupOptions.IsDevLike"/> (true for local <c>Development</c> and deployed <c>Dev</c>),
/// so intended Dev debuggability is actually present in Dev, while Production stays locked down.
/// </summary>
public sealed class DevToolsOptions
{
    public const string SectionName = "DevTools";

    /// <summary>
    /// Enables the test-only e2e cleanup endpoint. (Read today via raw configuration by
    /// <c>E2ECleanupEndpoint</c>; included here so the section binds cohesively.)
    /// </summary>
    public bool E2ECleanupEnabled { get; set; }

    /// <summary>
    /// When true, the global exception handler returns full exception detail in its response body.
    /// Null means "unset" — resolved at startup to <see cref="StartupOptions.IsDevLike"/>. Always
    /// forced off in Production regardless of configuration (mirrors the Swagger invariant), so
    /// production responses never leak stack traces or internal paths (FR-009/SC-007).
    /// </summary>
    public bool? ExposeExceptionDetail { get; set; }

    /// <summary>
    /// The resolved host environment name — not bound from configuration; stamped by
    /// <see cref="ApplyEnvironmentDefaults"/> so the validator can enforce environment-level
    /// invariants (015 US3: configuration flags cannot enable test machinery in Production).
    /// </summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>
    /// Applies environment-derived defaults after binding: when <see cref="ExposeExceptionDetail"/>
    /// was not set explicitly it defaults to dev-like; Production then forces it off unconditionally.
    /// </summary>
    public void ApplyEnvironmentDefaults(IHostEnvironment environment)
    {
        EnvironmentName = environment.EnvironmentName;
        ExposeExceptionDetail ??= StartupOptions.IsDevLike(environment);

        if (environment.IsProduction())
            ExposeExceptionDetail = false;
    }
}

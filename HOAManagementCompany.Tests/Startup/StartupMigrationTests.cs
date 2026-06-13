using HOAManagementCompany.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HOAManagementCompany.Tests.Startup;

/// <summary>
/// Exercises the config-driven startup migration path (009-dev-auto-deploy). With
/// <c>Startup:ApplyMigrations=true</c> and seeding off, Program.cs runs the migrations-only
/// branch (<c>MigrateAsync</c>) during boot — the code path a deployed <c>Dev</c> Cloud Run
/// service takes. The flag is supplied as an environment variable (not in-memory config) because
/// <c>StartupOptions.Resolve</c> reads configuration before <c>builder.Build()</c>, whereas
/// <c>WebApplicationFactory</c>'s in-memory overrides only apply at Build (the same reason the
/// harness injects the connection string via an env var).
/// </summary>
[Collection("Integration")]
public class StartupMigrationTests
{
    private readonly TestDatabaseFixture _fixture;

    public StartupMigrationTests(TestDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task App_RunsStartupMigrations_WhenApplyMigrationsEnabled()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _fixture.ConnectionString);
        Environment.SetEnvironmentVariable("Storage__ServiceUrl", _fixture.MinioEndpoint);
        Environment.SetEnvironmentVariable("Startup__ApplyMigrations", "true");
        try
        {
            await using var factory = new WebApplicationFactory<Program>();

            // CreateClient builds and starts the host, running the startup migration branch
            // (idempotent against the already-migrated Testcontainers database) before returning.
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health");
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            // Clear the process-global flag so later tests boot with default behavior.
            Environment.SetEnvironmentVariable("Startup__ApplyMigrations", null);
        }
    }
}

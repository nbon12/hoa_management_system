using System.Diagnostics.CodeAnalysis;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Seed;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Imperative startup side-effects (009-dev-auto-deploy) that require a fully built host and a live
/// database: the <c>--seed</c> CLI command and the config-driven apply-migrations/seed step.
/// <para>
/// Marked <see cref="ExcludeFromCodeCoverageAttribute"/> because these are bootstrap glue exercised
/// only against a real database in a deployed/integration environment — they cannot be unit-tested,
/// and booting them in the in-process test harness requires process-global env mutation that
/// pollutes other tests. The testable *decision* logic lives in <see cref="StartupOptions"/>, which
/// is unit-tested.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class StartupTasks
{
    /// <summary>
    /// Handles the <c>--seed</c> CLI flag. Returns an exit code when the flag was present (so the
    /// process should exit), or <c>null</c> to continue normal startup.
    /// </summary>
    public static async Task<int?> RunSeedCommandAsync(WebApplication app, string[] args)
    {
        if (!args.Contains("--seed"))
            return null;

        if (!StartupOptions.IsDevLike(app.Environment))
        {
            await Console.Error.WriteLineAsync(
                "ERROR: Seeder is restricted to the Development and Dev environments.");
            return 1;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
        return 0;
    }

    /// <summary>
    /// Applies migrations and/or seeds at startup per <paramref name="options"/>. <c>SeedAsync</c>
    /// runs <c>MigrateAsync</c> first and is idempotent, so a fresh database (local compose or a
    /// cold Dev deploy) becomes ready-to-use without a manual <c>--seed</c> step. The
    /// migrations-only branch covers environments that want the schema applied without synthetic
    /// seed data.
    /// </summary>
    public static async Task ApplyStartupDatabaseAsync(WebApplication app, StartupOptions options)
    {
        if (!options.SeedData && !options.ApplyMigrations)
            return;

        await using var scope = app.Services.CreateAsyncScope();

        if (options.SeedData)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync();

            // Refresh document PDFs in object storage — the bucket can be reset independently of
            // the database, and SeedAsync skips this on the already-seeded path.
            var storageInit = scope.ServiceProvider.GetRequiredService<DocumentStorageInitializer>();
            await storageInit.EnsureValidPdfsAsync();
        }
        else
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
        }
    }
}

using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Seed;

/// <summary>
/// Orchestrates all sub-seeders in dependency order. Idempotent — checks for resident@nekohoa.dev before inserting.
/// Restricted to the Development and Dev environments (enforced in Program.cs).
/// </summary>
public class DatabaseSeeder(
    ApplicationDbContext db,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IServiceProvider services,
    DocumentStorageInitializer documentStorageInitializer,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting database seed...");

        await db.Database.MigrateAsync(ct);

        var authSeeder = new AuthSeeder(db, services, logger);
        if (!await authSeeder.ShouldSeedAsync(ct))
        {
            logger.LogInformation("Seed data already present — skipping.");
            return;
        }

        var authResult = await authSeeder.SeedAsync(ct);
        await new PropertySeeder(db, authResult, logger).SeedAsync(ct);
        await new PaymentSeeder(db, authResult, logger).SeedAsync(ct);
        await new CommunitySeeder(db, authResult, logger).SeedAsync(ct);
        await new StorageSeeder(db, authResult, logger).SeedAsync(ct);
        await documentStorageInitializer.EnsureValidPdfsAsync(ct);

        logger.LogInformation("Seed complete.");
    }
}

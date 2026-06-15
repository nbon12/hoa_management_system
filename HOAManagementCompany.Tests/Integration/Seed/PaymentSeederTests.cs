using System;
using System.Linq;
using System.Threading.Tasks;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Seed;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using PropertyEntity = HOAManagementCompany.Domain.Entities.Property;

namespace HOAManagementCompany.Tests.Integration.Seed;

/// <summary>
/// Regression test for the production <see cref="PaymentSeeder"/> against a real Postgres (the same
/// Testcontainers fixture the other integration tests use). Before the fix the seeder never set
/// <c>LedgerEntry.Sequence</c>, so every seeded entry for a property defaulted to 0 and the batch
/// insert violated the unique index <c>IX_LedgerEntries_PropertyId_Sequence</c> with
/// PostgresException 23505 — which crashed app startup in any environment that seeds on boot (Dev).
/// The fixture's TestDataSeeder uses a separate code path, so the production seeder was previously
/// untested; this covers it directly. Runs in a rolled-back transaction so it leaves no data behind.
/// </summary>
[Collection("Integration")]
public class PaymentSeederTests(TestDatabaseFixture fixture)
{
    [Fact]
    public async Task PaymentSeeder_FreshProperty_AssignsUniqueSequences_AndPersists()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync();

        var property = new PropertyEntity
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"SEED-TEST-{Guid.NewGuid():N}",
            CommunityId = "SAKURA",
            CommunityName = "Sakura Hills",
            Address = "1 Test Way",
            City = "Testville",
            State = "CA",
            Zip = "00000",
            Lot = "1",
            Section = "A",
            FiscalYear = 2026,
            YearBuilt = 2000,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.01m,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var result = new SeedResult("seed-user-1", "seed-user-2", property.Id, Guid.NewGuid());

        // Before the fix this threw Npgsql.PostgresException 23505 on IX_LedgerEntries_PropertyId_Sequence.
        var ex = await Record.ExceptionAsync(() =>
            new PaymentSeeder(db, result, NullLogger.Instance).SeedAsync());
        Assert.Null(ex);

        var sequences = await db.LedgerEntries
            .Where(e => e.PropertyId == property.Id)
            .Select(e => e.Sequence)
            .OrderBy(s => s)
            .ToListAsync();

        Assert.True(sequences.Count >= 12, $"expected >= 12 ledger entries, got {sequences.Count}");
        Assert.Equal(sequences.Count, sequences.Distinct().Count()); // all sequences unique
        // Contiguous per-property numbering starting at 1, matching the runtime LedgerService path.
        Assert.Equal(Enumerable.Range(1, sequences.Count).Select(i => (long)i), sequences);

        await tx.RollbackAsync();
    }
}

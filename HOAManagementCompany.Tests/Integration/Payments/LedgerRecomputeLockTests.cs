using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Factories;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// 015 US1 acceptance scenario 4 (FR-004): <see cref="LedgerService.RecomputeBalancesAsync"/> must
/// participate in the same per-property serialization as appends. Without the advisory lock, an
/// append that lands while a recompute is repairing balances can base its running balance on a
/// stale predecessor, breaking the chain invariant
/// <c>RunningBalance[i] == RunningBalance[i-1] + Charge - Payment</c>.
/// </summary>
public class LedgerRecomputeLockTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private const int Rounds = 10;

    [Fact]
    public async Task RecomputeRacingAppend_NeverBreaksRunningBalanceChain()
    {
        for (var round = 0; round < Rounds; round++)
        {
            // Isolated property per round (parallel-/rerun-safe, no shared-seed coupling).
            Guid propertyId;
            using (var seedScope = Services.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var property = PropertyFactory.Create(
                    communityId: $"LRL-{Guid.NewGuid():N}"[..14],
                    accountNumber: $"LRL-{round}-{Guid.NewGuid():N}"[..18]);
                db.Properties.Add(property);
                await db.SaveChangesAsync();
                propertyId = property.Id;

                var ledger = seedScope.ServiceProvider.GetRequiredService<LedgerService>();
                for (var i = 0; i < 3; i++)
                    await ledger.AddCompensatingChargeAsync(propertyId, null, 100m,
                        LedgerEntryType.Adjustment, $"seed {i}");

                // Corrupt one stored balance directly — the exact state RecomputeBalancesAsync
                // exists to repair (out-of-order settlement) — so the recompute genuinely
                // rewrites values while the concurrent append races it.
                await db.Database.ExecuteSqlRawAsync(
                    """UPDATE "LedgerEntries" SET "RunningBalance" = "RunningBalance" + 500 WHERE "PropertyId" = {0} AND "Sequence" = 2""",
                    propertyId);
            }

            // Race: repair vs. append, each on its own scope/context like two real workers.
            async Task RecomputeAsync()
            {
                using var scope = Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LedgerService>()
                    .RecomputeBalancesAsync(propertyId);
            }
            async Task AppendAsync()
            {
                using var scope = Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LedgerService>()
                    .AddCompensatingChargeAsync(propertyId, null, 50m, LedgerEntryType.Adjustment, "raced append");
            }
            await Task.WhenAll(RecomputeAsync(), AppendAsync());

            // Invariant: the chain must be internally consistent regardless of interleaving.
            using var assertScope = Services.CreateScope();
            var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entries = await assertDb.LedgerEntries.AsNoTracking()
                .Where(e => e.PropertyId == propertyId)
                .OrderBy(e => e.Sequence)
                .ToListAsync();

            decimal running = 0m;
            foreach (var e in entries)
            {
                running += e.ChargeAmount - e.PaymentAmount;
                Assert.True(e.RunningBalance == running,
                    $"round {round}: sequence {e.Sequence} has RunningBalance {e.RunningBalance}, expected {running} — recompute/append raced without serialization");
            }
        }
    }
}

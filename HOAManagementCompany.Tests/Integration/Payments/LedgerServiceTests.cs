using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// The append-only ledger's self-healing path (SC-009): <see cref="LedgerService.RecomputeBalancesAsync"/>
/// is the canonical repair for out-of-order ACH settlements/refunds — it rewrites every
/// <c>RunningBalance</c> deterministically in <c>Sequence</c> order from <c>Charge − Payment</c>.
/// </summary>
public class LedgerServiceTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task RecomputeBalances_RepairsCorruptedRunningBalances()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<LedgerService>();

        // Append a couple of compensating entries (no transaction FK) on top of the seeded ledger.
        await ledger.AddCompensatingChargeAsync(PropertyId, null, 90m, LedgerEntryType.Adjustment, "drift A");
        await ledger.AddCompensatingChargeAsync(PropertyId, null, 35m, LedgerEntryType.Adjustment, "drift B");

        // Corrupt every stored RunningBalance so a naive read would be wrong. Clear the tracker so the
        // recompute reads the corrupted DB rows (as the reconcile sweep would in its own fresh scope).
        await db.LedgerEntries.Where(e => e.PropertyId == PropertyId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RunningBalance, -999_999m));
        db.ChangeTracker.Clear();

        var expected = await db.LedgerEntries
            .Where(e => e.PropertyId == PropertyId)
            .SumAsync(e => e.ChargeAmount - e.PaymentAmount);

        var finalBalance = await ledger.RecomputeBalancesAsync(PropertyId);

        Assert.Equal(expected, finalBalance);

        // The highest-sequence entry now carries the canonical final balance.
        var lastRunning = await db.LedgerEntries
            .Where(e => e.PropertyId == PropertyId)
            .OrderByDescending(e => e.Sequence)
            .Select(e => e.RunningBalance)
            .FirstAsync();
        Assert.Equal(expected, lastRunning);
        Assert.Equal(finalBalance, await ledger.GetCurrentBalanceAsync(PropertyId));
    }
}

using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Jobs;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Factories;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// 015 US1 FR-005: report-only detection of historical payment records whose status disagrees
/// with their ledger effects. Findings are surfaced as structured warnings; the data itself is
/// never mutated by detection.
/// </summary>
public class LedgerConsistencyDetectionTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private async Task<(Guid PropertyId, Guid OwnerId)> SeedIsolatedPropertyAsync(ApplicationDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = PropertyFactory.Create(communityId: $"LCD-{suffix}", accountNumber: $"LCD-{suffix}");
        var owner = OwnerFactory.Create(property.Id, email: $"lcd-{suffix}@test.dev");
        db.Properties.Add(property);
        db.Owners.Add(owner);
        await db.SaveChangesAsync();
        return (property.Id, owner.Id);
    }

    private static PaymentTransaction Txn(Guid propertyId, Guid ownerId, TransactionStatus status, decimal gross = 250m) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = propertyId,
        OwnerId = ownerId,
        StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
        StripeChargeId = $"ch_test_{Guid.NewGuid():N}",
        GrossAmount = gross,
        FeeAmount = 0m,
        Total = gross,
        Status = status,
        PaymentMethod = PaymentMethod.Ach,
    };

    [Fact]
    public async Task ReturnedWithoutReversal_IsReportedAndNotMutated()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (propertyId, ownerId) = await SeedIsolatedPropertyAsync(db);

        // Historical partial application: status says Returned, but no Reversal entry exists.
        var txn = Txn(propertyId, ownerId, TransactionStatus.Returned);
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();

        var reconciliation = scope.ServiceProvider.GetRequiredService<ReconciliationService>();
        var findings = await reconciliation.DetectLedgerInconsistenciesAsync();

        Assert.Contains(findings, f => f.PaymentTransactionId == txn.Id
            && f.Discrepancy == LedgerDiscrepancy.MissingLedgerEffect);
        Assert.Contains(LogSink.Events, e =>
            e.Level == Serilog.Events.LogEventLevel.Warning
            && e.MessageTemplate.Text.Contains("Ledger inconsistency")
            && e.Properties.ContainsKey("PaymentTransactionId")
            && e.Properties["PaymentTransactionId"].ToString().Contains(txn.Id.ToString()));

        // Report-only: nothing repaired, nothing appended.
        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Returned, txn.Status);
        Assert.Equal(0, await db.LedgerEntries.CountAsync(e => e.TransactionId == txn.Id));
    }

    [Fact]
    public async Task RefundSumMismatch_IsReported()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (propertyId, ownerId) = await SeedIsolatedPropertyAsync(db);

        // Cumulative says $100 refunded, but no Refund ledger entry exists.
        var txn = Txn(propertyId, ownerId, TransactionStatus.PartiallyRefunded);
        txn.CumulativeRefundedAmount = 100m;
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();

        var reconciliation = scope.ServiceProvider.GetRequiredService<ReconciliationService>();
        var findings = await reconciliation.DetectLedgerInconsistenciesAsync();

        Assert.Contains(findings, f => f.PaymentTransactionId == txn.Id
            && f.Discrepancy == LedgerDiscrepancy.RefundSumMismatch);
    }

    [Fact]
    public async Task ConsistentHistory_ProducesNoFindings()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (propertyId, ownerId) = await SeedIsolatedPropertyAsync(db);

        var txn = Txn(propertyId, ownerId, TransactionStatus.Succeeded);
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        var ledger = scope.ServiceProvider.GetRequiredService<HOAManagementCompany.Features.Payments.Ledger.LedgerService>();
        await ledger.AddPaymentAsync(propertyId, txn.Id, txn.GrossAmount, "payment");

        var reconciliation = scope.ServiceProvider.GetRequiredService<ReconciliationService>();
        var findings = await reconciliation.DetectLedgerInconsistenciesAsync();

        Assert.DoesNotContain(findings, f => f.PaymentTransactionId == txn.Id);
    }
}

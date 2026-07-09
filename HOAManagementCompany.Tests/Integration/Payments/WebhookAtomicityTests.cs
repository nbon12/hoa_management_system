using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// 015 US1 acceptance scenarios 1–2 (FR-001/FR-002): interrupt provider-event processing at an
/// intermediate persistence point, retry the delivery, and require exactly-once business effects
/// with the transaction status consistent with the ledger. Every handler write must be
/// all-or-nothing: a fault mid-handler must leave either nothing applied or everything applied —
/// never a committed ledger entry whose sibling effect (fee, receipt, status) is lost forever
/// behind a terminal-status guard.
/// </summary>
public class WebhookAtomicityTests(TestDatabaseFixture fixture) : FaultInjectedPaymentTestBase(fixture)
{
    [Fact]
    public async Task AchReturn_FaultBeforeNsfFee_RetryYieldsExactlyOneReversalAndOneFee()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Succeeded, PaymentMethod.Ach);

        // Crash at the persistence point that includes the NSF fee entry — after the reversal
        // logic has already run within the same business event.
        Fault.ArmOnce(ctx => ctx.ChangeTracker.Entries<LedgerEntry>()
            .Any(e => e.State == EntityState.Added && e.Entity.EntryType == LedgerEntryType.ReturnedPaymentFee));

        var evt = AchReturnEvent(txn.StripePaymentIntentId!);
        var crash = await DeliverAsync(evt);
        Assert.NotNull(crash);   // the injected crash must surface (delivery marked failed → retried)

        var retryError = await DeliverAsync(evt);   // reconcile-style redelivery, clean run
        Assert.Null(retryError);

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Returned, after.Status);
        Assert.Equal("R01", after.ReturnCode);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Reversal));
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.ReturnedPaymentFee));
    }

    [Fact]
    public async Task AchSettlement_FaultBeforeReceipt_RetryYieldsOnePaymentEntryAndOneReceipt()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Pending, PaymentMethod.Ach);

        Fault.ArmOnce(ctx => ctx.ChangeTracker.Entries<Receipt>()
            .Any(e => e.State == EntityState.Added));

        var evt = SucceededEvent(txn.StripePaymentIntentId!, txn.StripeChargeId!);
        Assert.NotNull(await DeliverAsync(evt));
        Assert.Null(await DeliverAsync(evt));

        var (after, entries, receipts) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Succeeded, after.Status);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Payment));
        Assert.Equal(1, receipts);
    }

    [Fact]
    public async Task Refund_FaultOnRefundEntry_RetryYieldsExactlyOneRefundAndCorrectCumulative()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Succeeded, PaymentMethod.Card, gross: 250m);

        Fault.ArmOnce(ctx => ctx.ChangeTracker.Entries<LedgerEntry>()
            .Any(e => e.State == EntityState.Added && e.Entity.EntryType == LedgerEntryType.Refund));

        var evt = RefundEvent(txn.StripeChargeId!, 10000);   // $100 cumulative
        Assert.NotNull(await DeliverAsync(evt));
        Assert.Null(await DeliverAsync(evt));

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.PartiallyRefunded, after.Status);
        Assert.Equal(100m, after.CumulativeRefundedAmount);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Refund));
        Assert.Equal(100m, entries.Single(e => e.EntryType == LedgerEntryType.Refund).ChargeAmount);
    }

    [Fact]
    public async Task DisputeCreated_FaultOnChargeback_RetryYieldsExactlyOneChargeback()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Succeeded, PaymentMethod.Card);

        Fault.ArmOnce(ctx => ctx.ChangeTracker.Entries<LedgerEntry>()
            .Any(e => e.State == EntityState.Added && e.Entity.EntryType == LedgerEntryType.Chargeback));

        var evt = DisputeCreatedEvent(txn.StripeChargeId!);
        Assert.NotNull(await DeliverAsync(evt));
        Assert.Null(await DeliverAsync(evt));

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Disputed, after.Status);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Chargeback));
    }

    [Fact]
    public async Task DisputeLost_FaultOnNsfFee_RetryYieldsExactlyOneFee()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Disputed, PaymentMethod.Card);

        Fault.ArmOnce(ctx => ctx.ChangeTracker.Entries<LedgerEntry>()
            .Any(e => e.State == EntityState.Added && e.Entity.EntryType == LedgerEntryType.ReturnedPaymentFee));

        var evt = DisputeClosedEvent(txn.StripeChargeId!, "lost");
        Assert.NotNull(await DeliverAsync(evt));
        Assert.Null(await DeliverAsync(evt));

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.DisputeLost, after.Status);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.ReturnedPaymentFee));
    }
}

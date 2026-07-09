using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// 015 US1 acceptance scenario 3 + the concurrent-duplicate edge case (FR-002): re-delivering a
/// fully-applied event — sequentially or in parallel from two workers — must be a no-op. The
/// idempotency guard and the write it guards must commit together on a row-locked transaction,
/// so two racing deliveries can never both pass the guard.
/// </summary>
public class WebhookIdempotencyTests(TestDatabaseFixture fixture) : FaultInjectedPaymentTestBase(fixture)
{
    [Fact]
    public async Task AchReturn_SequentialRedelivery_IsNoOp()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Succeeded, PaymentMethod.Ach);
        var evt = AchReturnEvent(txn.StripePaymentIntentId!);

        Assert.Null(await DeliverAsync(evt));
        Assert.Null(await DeliverAsync(evt));

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Returned, after.Status);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Reversal));
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.ReturnedPaymentFee));
    }

    [Fact]
    public async Task Refund_RedeliveryWithSameCumulative_AddsNothing()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Succeeded, PaymentMethod.Card, gross: 250m);
        var evt = RefundEvent(txn.StripeChargeId!, 25000);

        Assert.Null(await DeliverAsync(evt));
        Assert.Null(await DeliverAsync(evt));

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Refunded, after.Status);
        Assert.Equal(250m, after.CumulativeRefundedAmount);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Refund));
    }

    [Fact]
    public async Task AchReturn_ConcurrentDuplicateDelivery_YieldsExactlyOneReversalAndOneFee()
    {
        // Spec edge case: two workers pick up the same event simultaneously (live webhook +
        // reconcile retry). Each DeliverAsync runs in its own scope → its own DbContext, like
        // two real workers. Both must not pass the status guard.
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Succeeded, PaymentMethod.Ach);
        var evt = AchReturnEvent(txn.StripePaymentIntentId!);

        var results = await Task.WhenAll(DeliverAsync(evt), DeliverAsync(evt));
        // Neither worker may corrupt state; transient serialization aborts are acceptable only
        // if the ledger still ends exactly-once (the reconcile loop would retry that worker).
        _ = results;

        var (after, entries, _) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Returned, after.Status);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Reversal));
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.ReturnedPaymentFee));
    }

    [Fact]
    public async Task Settlement_ConcurrentDuplicateDelivery_YieldsOnePaymentEntryAndOneReceipt()
    {
        var txn = await SeedIsolatedTransactionAsync(TransactionStatus.Pending, PaymentMethod.Ach);
        var evt = SucceededEvent(txn.StripePaymentIntentId!, txn.StripeChargeId!);

        await Task.WhenAll(DeliverAsync(evt), DeliverAsync(evt));

        var (after, entries, receipts) = await SnapshotAsync(txn.Id);
        Assert.Equal(TransactionStatus.Succeeded, after.Status);
        Assert.Equal(1, entries.Count(e => e.EntryType == LedgerEntryType.Payment));
        Assert.Equal(1, receipts);
    }
}

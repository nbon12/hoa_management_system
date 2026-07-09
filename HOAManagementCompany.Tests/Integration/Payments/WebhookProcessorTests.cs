using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Webhooks;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using HOAManagementCompany.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// Exercises the full Stripe webhook lifecycle directly against <see cref="WebhookProcessor"/>:
/// ACH settlement, failures/returns, cumulative refunds, and disputes. Each handler appends
/// compensating ledger entries (never mutates) and is idempotent (FR-014, FR-017).
/// </summary>
public class WebhookProcessorTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private async Task<PaymentTransaction> SeedTransactionAsync(
        ApplicationDbContext db, TransactionStatus status, PaymentMethod method,
        decimal gross = 250m, string? chargeId = null)
    {
        var ownerId = await db.Owners.Where(o => o.PropertyId == PropertyId).Select(o => o.Id).FirstAsync();
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = PropertyId,
            OwnerId = ownerId,
            StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            StripeChargeId = chargeId ?? $"ch_test_{Guid.NewGuid():N}",
            GrossAmount = gross,
            FeeAmount = 0m,
            Total = gross,
            Status = status,
            PaymentMethod = method,
        };
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    // Neutral provider events (015 FR-021): handlers are exercised without any SDK types —
    // Stripe→neutral mapping itself is covered by ProviderEventMappingTests in the unit tier.
    private static PaymentProviderEvent SucceededEvent(string intentId, string chargeId) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.PaymentSucceeded, "payment_intent.succeeded",
        PaymentIntentId: intentId, LatestChargeId: chargeId);

    private static PaymentProviderEvent FailedEvent(string intentId, string code) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.PaymentFailed, "payment_intent.payment_failed",
        PaymentIntentId: intentId, FailureCode: code, FailureMessage: "declined");

    private static PaymentProviderEvent RefundEvent(string chargeId, long amountRefundedCents) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.Refunded, "charge.refunded",
        ChargeId: chargeId, AmountRefunded: amountRefundedCents / 100m);

    private static PaymentProviderEvent DisputeCreatedEvent(string chargeId) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.DisputeCreated, "charge.dispute.created",
        ChargeId: chargeId, DisputeId: $"dp_{Guid.NewGuid():N}", DisputeStatus: "warning_needs_response");

    private static PaymentProviderEvent DisputeClosedEvent(string chargeId, string status) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.DisputeClosed, "charge.dispute.closed",
        ChargeId: chargeId, DisputeId: $"dp_{Guid.NewGuid():N}", DisputeStatus: status);

    [Fact]
    public async Task PaymentIntentSucceeded_PendingAch_SettlesWithLedgerAndReceipt()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Pending, PaymentMethod.Ach);

        await processor.ProcessAsync(SucceededEvent(txn.StripePaymentIntentId!, txn.StripeChargeId!));

        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Succeeded, txn.Status);
        Assert.True(await db.LedgerEntries.AnyAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Payment));
        Assert.True(await db.Receipts.AnyAsync(r => r.TransactionId == txn.Id));
    }

    [Fact]
    public async Task PaymentIntentSucceeded_Idempotent_NoDuplicateLedger()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Pending, PaymentMethod.Ach);

        await processor.ProcessAsync(SucceededEvent(txn.StripePaymentIntentId!, txn.StripeChargeId!));
        await processor.ProcessAsync(SucceededEvent(txn.StripePaymentIntentId!, txn.StripeChargeId!));

        var paymentEntries = await db.LedgerEntries
            .CountAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Payment);
        Assert.Equal(1, paymentEntries);
    }

    [Fact]
    public async Task PaymentIntentFailed_PendingCard_MarksFailed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Pending, PaymentMethod.Card);

        await processor.ProcessAsync(FailedEvent(txn.StripePaymentIntentId!, "card_declined"));

        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Failed, txn.Status);
        Assert.Equal("card_declined", txn.FailureCode);
    }

    [Fact]
    public async Task PaymentIntentFailed_SettledAch_ReturnsWithReversalAndNsf()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Succeeded, PaymentMethod.Ach);

        await processor.ProcessAsync(FailedEvent(txn.StripePaymentIntentId!, "R01"));

        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Returned, txn.Status);
        Assert.Equal("R01", txn.ReturnCode);
        Assert.True(await db.LedgerEntries.AnyAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Reversal));
        Assert.True(await db.LedgerEntries.AnyAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.ReturnedPaymentFee));
    }

    [Fact]
    public async Task ChargeRefunded_CumulativeDeltas_PartialThenFull()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Succeeded, PaymentMethod.Card, gross: 250m);

        await processor.ProcessAsync(RefundEvent(txn.StripeChargeId!, 10000));   // $100 cumulative
        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.PartiallyRefunded, txn.Status);
        Assert.Equal(100m, txn.CumulativeRefundedAmount);

        await processor.ProcessAsync(RefundEvent(txn.StripeChargeId!, 25000));   // $250 cumulative
        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Refunded, txn.Status);
        Assert.Equal(250m, txn.CumulativeRefundedAmount);

        var refundEntries = await db.LedgerEntries
            .CountAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Refund);
        Assert.Equal(2, refundEntries);   // $100 + $150 deltas

        // Idempotent: a repeat of the same cumulative amount adds nothing.
        await processor.ProcessAsync(RefundEvent(txn.StripeChargeId!, 25000));
        Assert.Equal(2, await db.LedgerEntries.CountAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Refund));
    }

    [Fact]
    public async Task Dispute_CreatedThenWon_RestoresFunds()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Succeeded, PaymentMethod.Card);

        await processor.ProcessAsync(DisputeCreatedEvent(txn.StripeChargeId!));
        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Disputed, txn.Status);
        Assert.True(await db.LedgerEntries.AnyAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Chargeback));

        await processor.ProcessAsync(DisputeClosedEvent(txn.StripeChargeId!, "won"));
        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Succeeded, txn.Status);
    }

    [Fact]
    public async Task Dispute_Lost_MarksDisputeLostWithNsf()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await SeedTransactionAsync(db, TransactionStatus.Disputed, PaymentMethod.Card);

        await processor.ProcessAsync(DisputeClosedEvent(txn.StripeChargeId!, "lost"));
        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.DisputeLost, txn.Status);
        Assert.True(await db.LedgerEntries.AnyAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.ReturnedPaymentFee));
    }

    [Fact]
    public async Task UnknownEventType_IsIgnored()
    {
        using var scope = Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var evt = new PaymentProviderEvent($"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.Ignored, "customer.created");
        await processor.ProcessAsync(evt);   // must not throw
    }

    [Fact]
    public async Task UnknownTransactionReference_IsIgnored()
    {
        using var scope = Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        await processor.ProcessAsync(SucceededEvent($"pi_test_{Guid.NewGuid():N}", $"ch_test_{Guid.NewGuid():N}"));
    }
}

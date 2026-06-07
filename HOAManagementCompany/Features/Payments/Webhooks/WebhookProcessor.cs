using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Features.Payments.Webhooks;

// <!-- REPOWISE:START domain=payments-webhooks -->
// Durable Stripe webhook lifecycle handling: payment success/failure (incl. deferred ACH ledger),
// partial/cumulative refunds, dispute create/resolve, and ACH return-after-settlement. Each handler
// is idempotent via terminal-status guards and writes compensating (never mutating) ledger entries.
// <!-- REPOWISE:END -->

/// <summary>
/// Applies a verified Stripe event to local state. Resolves the owning
/// <see cref="PaymentTransaction"/> by Stripe reference, never mutates ledger rows (only appends
/// compensating entries), and is safe to re-run (FR-014, FR-017).
/// </summary>
public sealed class WebhookProcessor(
    ApplicationDbContext db,
    LedgerService ledger,
    PaymentConfigService config,
    IStripeGateway gateway,
    AlertService alerts,
    PaymentMetrics metrics,
    ILogger<WebhookProcessor> logger)
{
    public async Task ProcessAsync(Event evt, CancellationToken ct = default)
    {
        switch (evt.Type)
        {
            case "payment_intent.succeeded":
                await HandleSucceededAsync((PaymentIntent)evt.Data.Object, ct);
                break;
            case "payment_intent.payment_failed":
                await HandleFailedAsync((PaymentIntent)evt.Data.Object, ct);
                break;
            case "charge.refunded":
            case "charge.refund.updated":
                await HandleRefundAsync((Charge)evt.Data.Object, ct);
                break;
            case "charge.dispute.created":
                await HandleDisputeCreatedAsync((Dispute)evt.Data.Object, ct);
                break;
            case "charge.dispute.closed":
                await HandleDisputeClosedAsync((Dispute)evt.Data.Object, ct);
                break;
            default:
                logger.LogInformation("Unhandled Stripe event type {EventType} ({EventId})", evt.Type, evt.Id);
                break;
        }
    }

    private async Task HandleSucceededAsync(PaymentIntent pi, CancellationToken ct)
    {
        var txn = await FindByIntentAsync(pi.Id, ct);
        if (txn is null) { LogUnknown("payment_intent.succeeded", pi.Id); return; }
        await SettleSucceededAsync(txn, pi.LatestChargeId, ct);
    }

    /// <summary>
    /// Marks a transaction settled: attaches settlement references and, for a still-Pending ACH
    /// charge, writes the deferred ledger entry + receipt. Idempotent (the Pending guard prevents a
    /// second ledger row). Reused by the reconciliation sweep when a webhook is missed (FR-033).
    /// </summary>
    public async Task SettleSucceededAsync(PaymentTransaction txn, string? chargeId, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(chargeId)) txn.StripeChargeId = chargeId;
        await AttachSettlementAsync(txn, ct);

        // Card already settled at confirm; only ACH defers the ledger entry until settlement (FR-007 ACH).
        if (txn.Status == TransactionStatus.Pending)
        {
            txn.Status = TransactionStatus.Succeeded;
            await ledger.AddPaymentAsync(txn.PropertyId, txn.Id, txn.GrossAmount,
                $"Online Payment – ACH – {txn.StripeChargeId}", ct: ct);
            await EnsureReceiptAsync(txn, ct);
            metrics.RecordPaymentProcessed("succeeded");
        }
        txn.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleFailedAsync(PaymentIntent pi, CancellationToken ct)
    {
        var txn = await FindByIntentAsync(pi.Id, ct);
        if (txn is null) { LogUnknown("payment_intent.payment_failed", pi.Id); return; }

        var error = pi.LastPaymentError;
        if (txn.Status == TransactionStatus.Succeeded && txn.PaymentMethod == PaymentMethod.Ach)
        {
            // A previously-settled ACH debit was returned (e.g. R01 insufficient funds) — FR-014c.
            txn.Status = TransactionStatus.Returned;
            txn.ReturnCode = error?.Code;
            await ledger.AddCompensatingChargeAsync(txn.PropertyId, txn.Id, txn.GrossAmount,
                LedgerEntryType.Reversal, $"ACH return – {txn.ReturnCode ?? "unknown"}", ct: ct);
            await ApplyNsfFeeAsync(txn, ct);
            // A settled debit was clawed back — always alert the owner who opted in (FR-014c).
            await alerts.EnqueueFailureAlertAsync(txn, txn.ReturnCode, ct);
            metrics.RecordPaymentProcessed("returned");
        }
        else if (txn.Status is TransactionStatus.Pending or TransactionStatus.Succeeded)
        {
            txn.Status = TransactionStatus.Failed;
            txn.FailureCode = error?.Code;
            txn.FailureMessage = Scrub(error?.Message);
            // Only recurring (off-session) failures alert; a one-time failure is shown inline (FR-015).
            if (txn.IsRecurring)
                await alerts.EnqueueFailureAlertAsync(txn, txn.FailureCode, ct);
            metrics.RecordPaymentProcessed("failed");
        }
        txn.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleRefundAsync(Charge charge, CancellationToken ct)
    {
        var txn = await FindByChargeAsync(charge.Id, ct);
        if (txn is null) { LogUnknown("charge.refunded", charge.Id); return; }

        var cumulative = charge.AmountRefunded / 100m;       // Stripe's cumulative source of truth (FR-014b).
        var delta = cumulative - txn.CumulativeRefundedAmount;
        if (delta <= 0m) return;                              // Idempotent: already applied.

        txn.CumulativeRefundedAmount = cumulative;
        txn.Status = cumulative >= txn.Total ? TransactionStatus.Refunded : TransactionStatus.PartiallyRefunded;
        metrics.RecordPaymentProcessed(txn.Status == TransactionStatus.Refunded ? "refunded" : "partially_refunded");
        // Fee is retained on refund (mirrors Stripe keeping its processing fee, FR-004d).
        await ledger.AddCompensatingChargeAsync(txn.PropertyId, txn.Id, delta,
            LedgerEntryType.Refund, $"Refund – {charge.Id}", ct: ct);
        txn.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleDisputeCreatedAsync(Dispute dispute, CancellationToken ct)
    {
        var txn = await FindByChargeAsync(dispute.ChargeId, ct);
        if (txn is null) { LogUnknown("charge.dispute.created", dispute.ChargeId); return; }
        if (txn.Status == TransactionStatus.Disputed) return;

        txn.Status = TransactionStatus.Disputed;
        await ledger.AddCompensatingChargeAsync(txn.PropertyId, txn.Id, txn.GrossAmount,
            LedgerEntryType.Chargeback, $"Dispute opened – {dispute.Id}", ct: ct);
        metrics.RecordPaymentProcessed("disputed");
        txn.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleDisputeClosedAsync(Dispute dispute, CancellationToken ct)
    {
        var txn = await FindByChargeAsync(dispute.ChargeId, ct);
        if (txn is null) { LogUnknown("charge.dispute.closed", dispute.ChargeId); return; }

        if (dispute.Status == "won")
        {
            // Funds restored — undo the chargeback reversal and return to Succeeded.
            txn.Status = TransactionStatus.Succeeded;
            await ledger.AddPaymentAsync(txn.PropertyId, txn.Id, txn.GrossAmount,
                $"Dispute won – funds restored – {dispute.Id}", ct: ct);
            metrics.RecordPaymentProcessed("dispute_won");
        }
        else if (dispute.Status == "lost")
        {
            txn.Status = TransactionStatus.DisputeLost;   // Reversal already stands from creation.
            await ApplyNsfFeeAsync(txn, ct);
            metrics.RecordPaymentProcessed("dispute_lost");
        }
        txn.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task AttachSettlementAsync(PaymentTransaction txn, CancellationToken ct)
    {
        if (txn.StripeChargeId is null || txn.StripeBalanceTransactionId is not null) return;
        var settlement = await gateway.GetChargeAsync(txn.StripeChargeId, ct);
        if (settlement is null) return;
        txn.StripeBalanceTransactionId = settlement.BalanceTransactionId;
        txn.ProcessorFeeAmount = settlement.ProcessorFeeAmount;
        txn.StripePayoutId = settlement.PayoutId;
    }

    private async Task ApplyNsfFeeAsync(PaymentTransaction txn, CancellationToken ct)
    {
        var cfg = await config.GetForPropertyAsync(txn.PropertyId, ct);
        if (!cfg.NsfFeeEnabled || cfg.NsfFeeAmount <= 0m) return;
        await ledger.AddCompensatingChargeAsync(txn.PropertyId, txn.Id, cfg.NsfFeeAmount,
            LedgerEntryType.ReturnedPaymentFee, "Returned payment fee (NSF)", ct: ct);
    }

    private async Task EnsureReceiptAsync(PaymentTransaction txn, CancellationToken ct)
    {
        if (await db.Receipts.AnyAsync(r => r.TransactionId == txn.Id, ct)) return;
        db.Receipts.Add(ReceiptFactory.Create(txn));
    }

    private Task<PaymentTransaction?> FindByIntentAsync(string intentId, CancellationToken ct) =>
        db.PaymentTransactions.FirstOrDefaultAsync(t => t.StripePaymentIntentId == intentId, ct);

    private Task<PaymentTransaction?> FindByChargeAsync(string chargeId, CancellationToken ct) =>
        db.PaymentTransactions.FirstOrDefaultAsync(t => t.StripeChargeId == chargeId, ct);

    private void LogUnknown(string eventType, string reference) =>
        logger.LogWarning("{EventType} referenced unknown transaction {Reference}; no mutation applied", eventType, reference);

    private static string? Scrub(string? message) =>
        string.IsNullOrWhiteSpace(message) ? null : message.Length > 200 ? message[..200] : message;
}

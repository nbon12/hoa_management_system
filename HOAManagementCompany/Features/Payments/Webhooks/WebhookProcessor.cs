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
// partial/cumulative refunds, dispute create/resolve, and ACH return-after-settlement. Every
// handler routes its writes through PaymentRecorder.ApplyAsync (015 FR-001/FR-002): the
// terminal-status/cumulative guard is re-evaluated on a FOR UPDATE-locked row inside the same
// transaction as the ledger entries, fees, alerts, receipt, and status change — so an interrupted
// delivery retries to exactly-once effects, and concurrent duplicate deliveries serialize.
// <!-- REPOWISE:END -->

/// <summary>
/// Applies a verified Stripe event to local state. Resolves the owning
/// <see cref="PaymentTransaction"/> by Stripe reference, never mutates ledger rows (only appends
/// compensating entries), and is safe to re-run and to race (FR-014, FR-017, 015 US1).
/// External provider lookups happen before the atomic unit opens — never inside it (Neon: short
/// transactions, no network under a lock).
/// </summary>
public sealed class WebhookProcessor(
    ApplicationDbContext db,
    LedgerService ledger,
    PaymentConfigService config,
    IStripeGateway gateway,
    AlertService alerts,
    PaymentRecorder recorder,
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
    /// charge, writes the deferred ledger entry + receipt — all in one atomic unit, so a fault at
    /// any point leaves nothing applied and the retry completes everything (FR-001). Idempotent
    /// via the in-transaction Pending guard. Reused by the reconciliation sweep (FR-033).
    /// </summary>
    public async Task SettleSucceededAsync(PaymentTransaction txn, string? chargeId, CancellationToken ct = default)
    {
        // Provider lookup OUTSIDE the transaction; results are applied inside it.
        var chargeRef = chargeId ?? txn.StripeChargeId;
        var settlement = chargeRef is not null && txn.StripeBalanceTransactionId is null
            ? await gateway.GetChargeAsync(chargeRef, ct)
            : null;

        var settled = false;
        await recorder.ApplyAsync(txn.Id, async (t, innerCt) =>
        {
            if (!string.IsNullOrEmpty(chargeId)) t.StripeChargeId = chargeId;
            if (settlement is not null && t.StripeBalanceTransactionId is null)
            {
                t.StripeBalanceTransactionId = settlement.BalanceTransactionId;
                t.ProcessorFeeAmount = settlement.ProcessorFeeAmount;
                t.StripePayoutId = settlement.PayoutId;
            }

            // Card already settled at confirm; only ACH defers the ledger entry until settlement.
            if (t.Status == TransactionStatus.Pending)
            {
                t.Status = TransactionStatus.Succeeded;
                await ledger.AddPaymentAsync(t.PropertyId, t.Id, t.GrossAmount,
                    $"Online Payment – ACH – {t.StripeChargeId}", ct: innerCt);
                if (!await db.Receipts.AnyAsync(r => r.TransactionId == t.Id, innerCt))
                    db.Receipts.Add(ReceiptFactory.Create(t));
                settled = true;
            }
        }, ct);
        if (settled) metrics.RecordPaymentProcessed("succeeded");
    }

    private async Task HandleFailedAsync(PaymentIntent pi, CancellationToken ct)
    {
        var txn = await FindByIntentAsync(pi.Id, ct);
        if (txn is null) { LogUnknown("payment_intent.payment_failed", pi.Id); return; }

        var error = pi.LastPaymentError;
        string? outcome = null;
        await recorder.ApplyAsync(txn.Id, async (t, innerCt) =>
        {
            outcome = null;   // reset on execution-strategy re-run
            if (t.Status == TransactionStatus.Succeeded && t.PaymentMethod == PaymentMethod.Ach)
            {
                // A previously-settled ACH debit was returned (e.g. R01 insufficient funds) — FR-014c.
                // Reversal + NSF fee + alert + status commit together (015 FR-001).
                t.Status = TransactionStatus.Returned;
                t.ReturnCode = error?.Code;
                await ledger.AddCompensatingChargeAsync(t.PropertyId, t.Id, t.GrossAmount,
                    LedgerEntryType.Reversal, $"ACH return – {t.ReturnCode ?? "unknown"}", ct: innerCt);
                await ApplyNsfFeeAsync(t, innerCt);
                // A settled debit was clawed back — always alert the owner who opted in (FR-014c).
                await alerts.EnqueueFailureAlertAsync(t, t.ReturnCode, innerCt);
                outcome = "returned";
            }
            else if (t.Status is TransactionStatus.Pending or TransactionStatus.Succeeded)
            {
                t.Status = TransactionStatus.Failed;
                t.FailureCode = error?.Code;
                t.FailureMessage = Scrub(error?.Message);
                // Only recurring (off-session) failures alert; a one-time failure is shown inline (FR-015).
                if (t.IsRecurring)
                    await alerts.EnqueueFailureAlertAsync(t, t.FailureCode, innerCt);
                outcome = "failed";
            }
        }, ct);
        if (outcome is not null) metrics.RecordPaymentProcessed(outcome);
    }

    private async Task HandleRefundAsync(Charge charge, CancellationToken ct)
    {
        var txn = await FindByChargeAsync(charge.Id, ct);
        if (txn is null) { LogUnknown("charge.refunded", charge.Id); return; }

        string? outcome = null;
        await recorder.ApplyAsync(txn.Id, async (t, innerCt) =>
        {
            outcome = null;
            var cumulative = charge.AmountRefunded / 100m;    // Stripe's cumulative source of truth (FR-014b).
            var delta = cumulative - t.CumulativeRefundedAmount;
            if (delta <= 0m) return;                          // Idempotent: guard holds the row lock.

            t.CumulativeRefundedAmount = cumulative;
            t.Status = cumulative >= t.Total ? TransactionStatus.Refunded : TransactionStatus.PartiallyRefunded;
            // Fee is retained on refund (mirrors Stripe keeping its processing fee, FR-004d).
            await ledger.AddCompensatingChargeAsync(t.PropertyId, t.Id, delta,
                LedgerEntryType.Refund, $"Refund – {charge.Id}", ct: innerCt);
            outcome = t.Status == TransactionStatus.Refunded ? "refunded" : "partially_refunded";
        }, ct);
        if (outcome is not null) metrics.RecordPaymentProcessed(outcome);
    }

    private async Task HandleDisputeCreatedAsync(Dispute dispute, CancellationToken ct)
    {
        var txn = await FindByChargeAsync(dispute.ChargeId, ct);
        if (txn is null) { LogUnknown("charge.dispute.created", dispute.ChargeId); return; }

        var applied = false;
        await recorder.ApplyAsync(txn.Id, async (t, innerCt) =>
        {
            applied = false;
            if (t.Status == TransactionStatus.Disputed) return;
            t.Status = TransactionStatus.Disputed;
            await ledger.AddCompensatingChargeAsync(t.PropertyId, t.Id, t.GrossAmount,
                LedgerEntryType.Chargeback, $"Dispute opened – {dispute.Id}", ct: innerCt);
            applied = true;
        }, ct);
        if (applied) metrics.RecordPaymentProcessed("disputed");
    }

    private async Task HandleDisputeClosedAsync(Dispute dispute, CancellationToken ct)
    {
        var txn = await FindByChargeAsync(dispute.ChargeId, ct);
        if (txn is null) { LogUnknown("charge.dispute.closed", dispute.ChargeId); return; }

        string? outcome = null;
        await recorder.ApplyAsync(txn.Id, async (t, innerCt) =>
        {
            outcome = null;
            if (dispute.Status == "won" && t.Status == TransactionStatus.Disputed)
            {
                // Funds restored — undo the chargeback reversal and return to Succeeded.
                t.Status = TransactionStatus.Succeeded;
                await ledger.AddPaymentAsync(t.PropertyId, t.Id, t.GrossAmount,
                    $"Dispute won – funds restored – {dispute.Id}", ct: innerCt);
                outcome = "dispute_won";
            }
            else if (dispute.Status == "lost" && t.Status == TransactionStatus.Disputed)
            {
                t.Status = TransactionStatus.DisputeLost;   // Reversal already stands from creation.
                await ApplyNsfFeeAsync(t, innerCt);
                outcome = "dispute_lost";
            }
        }, ct);
        if (outcome is not null) metrics.RecordPaymentProcessed(outcome);
    }

    private async Task ApplyNsfFeeAsync(PaymentTransaction txn, CancellationToken ct)
    {
        var cfg = await config.GetForPropertyAsync(txn.PropertyId, ct);
        if (!cfg.NsfFeeEnabled || cfg.NsfFeeAmount <= 0m) return;
        await ledger.AddCompensatingChargeAsync(txn.PropertyId, txn.Id, cfg.NsfFeeAmount,
            LedgerEntryType.ReturnedPaymentFee, "Returned payment fee (NSF)", ct: ct);
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

using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Webhooks;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sentry;
using Stripe;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Features.Payments.Jobs;

/// <summary>How a transaction's status disagrees with its ledger effects (015 FR-005).</summary>
public enum LedgerDiscrepancy
{
    /// <summary>A terminal status implies a ledger entry that does not exist.</summary>
    MissingLedgerEffect,
    /// <summary>A ledger effect that must be unique exists more than once.</summary>
    DuplicateLedgerEffect,
    /// <summary>The transaction's cumulative refunded amount disagrees with the summed refund entries.</summary>
    RefundSumMismatch,
}

/// <summary>
/// One report-only detection finding. Never persisted — surfaced through structured logs and a
/// Sentry alert so a human decides any correction (015 FR-005: detect and report, never mutate).
/// Carries ids and a discrepancy kind only; no monetary amounts beyond the delta description.
/// </summary>
public sealed record LedgerInconsistencyFinding(
    Guid PaymentTransactionId,
    Guid PropertyId,
    LedgerDiscrepancy Discrepancy,
    string Detail);

// <!-- REPOWISE:START domain=payments-jobs -->
// Reconciliation sweep (Cloud Scheduler-triggered): resolves ACH transactions stuck Pending past
// their settlement window against Stripe (system-of-record), and retries undelivered/failed
// webhook events from the durable inbox, dead-lettering after a threshold (FR-032/FR-033).
// <!-- REPOWISE:END -->

/// <summary>Backstop for missed webhooks and stuck ACH settlements. Safe to re-run.</summary>
public sealed class ReconciliationService(
    ApplicationDbContext db,
    IStripeGateway gateway,
    WebhookProcessor processor,
    IOptions<PaymentsOptions> options,
    ILogger<ReconciliationService> logger)
{
    private const int MaxWebhookAttempts = 5;

    /// <summary>
    /// Resolves ACH transactions still <see cref="TransactionStatus.Pending"/> beyond the
    /// configured window by asking Stripe for the current PaymentIntent status (FR-033).
    /// Returns the number of transactions resolved.
    /// </summary>
    public async Task<int> ResolvePendingAchAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-options.Value.ReconcilePendingAchAfterHours);
        var pending = await db.PaymentTransactions
            .Where(t => t.Status == TransactionStatus.Pending
                     && t.PaymentMethod == PaymentMethod.Ach
                     && t.CreatedAt <= cutoff
                     && t.StripePaymentIntentId != null)
            .ToListAsync(ct);

        var resolved = 0;
        foreach (var txn in pending)
        {
            var pi = await gateway.GetPaymentIntentAsync(txn.StripePaymentIntentId!, ct);
            if (pi.Status == "succeeded")
            {
                await processor.SettleSucceededAsync(txn, pi.LatestChargeId, ct);
                resolved++;
            }
            else if (pi.Status is "canceled" or "requires_payment_method")
            {
                txn.Status = TransactionStatus.Failed;
                txn.FailureCode = pi.FailureCode;
                txn.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                resolved++;
            }
        }
        if (resolved > 0) logger.LogInformation("Reconcile resolved {Count} pending ACH transaction(s)", resolved);
        return resolved;
    }

    /// <summary>
    /// Re-processes durable inbox events still in <see cref="WebhookProcessingStatus.Received"/>,
    /// dead-lettering after <see cref="MaxWebhookAttempts"/> failed attempts. Returns count processed.
    /// </summary>
    public async Task<int> RetryPendingWebhooksAsync(CancellationToken ct = default)
    {
        var pending = await db.WebhookEventInbox
            .Where(w => w.Status == WebhookProcessingStatus.Received)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var inbox in pending)
        {
            inbox.Attempts++;
            try
            {
                var evt = EventUtility.ParseEvent(inbox.Payload);
                await processor.ProcessAsync(evt, ct);
                inbox.Status = WebhookProcessingStatus.Processed;
                inbox.ProcessedAt = DateTimeOffset.UtcNow;
                processed++;
            }
            catch (Exception ex)
            {
                inbox.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                if (inbox.Attempts >= MaxWebhookAttempts)
                {
                    inbox.Status = WebhookProcessingStatus.DeadLettered;
                    logger.LogError(ex, "Webhook {EventId} dead-lettered after {Attempts} attempts", inbox.StripeEventId, inbox.Attempts);
                }
            }
            await db.SaveChangesAsync(ct);
        }
        return processed;
    }

    /// <summary>
    /// Report-only consistency scan (015 FR-005): flags historical payment records whose status
    /// disagrees with their ledger effects — missing/duplicate reversal for a Returned txn,
    /// missing payment entry for a Succeeded txn, refund-entry sum ≠ cumulative refunded amount.
    /// Runs once at cutover and then on every reconcile sweep. Emits a structured Serilog warning
    /// and a Sentry alert per finding; never appends, repairs, or mutates anything.
    /// </summary>
    public async Task<IReadOnlyList<LedgerInconsistencyFinding>> DetectLedgerInconsistenciesAsync(
        CancellationToken ct = default)
    {
        var findings = new List<LedgerInconsistencyFinding>();

        // Ledger effect counts per transaction, grouped once (report job — full scan is fine).
        var effectCounts = await db.LedgerEntries
            .Where(e => e.TransactionId != null)
            .GroupBy(e => new { e.TransactionId, e.EntryType })
            .Select(g => new { g.Key.TransactionId, g.Key.EntryType, Count = g.Count(), Sum = g.Sum(x => x.ChargeAmount) })
            .ToListAsync(ct);
        var byTxn = effectCounts.ToLookup(x => x.TransactionId!.Value);

        var terminal = await db.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Succeeded
                     || t.Status == TransactionStatus.Returned
                     || t.Status == TransactionStatus.Refunded
                     || t.Status == TransactionStatus.PartiallyRefunded)
            .Select(t => new { t.Id, t.PropertyId, t.Status, t.CumulativeRefundedAmount })
            .ToListAsync(ct);

        foreach (var t in terminal)
        {
            var effects = byTxn[t.Id].ToList();
            int Count(LedgerEntryType type) => effects.Where(e => e.EntryType == type).Sum(e => e.Count);
            decimal Sum(LedgerEntryType type) => effects.Where(e => e.EntryType == type).Sum(e => e.Sum);

            if (t.Status == TransactionStatus.Succeeded && Count(LedgerEntryType.Payment) == 0)
                findings.Add(new(t.Id, t.PropertyId, LedgerDiscrepancy.MissingLedgerEffect,
                    "Status Succeeded but no Payment ledger entry exists"));

            if (t.Status == TransactionStatus.Returned)
            {
                var reversals = Count(LedgerEntryType.Reversal);
                if (reversals == 0)
                    findings.Add(new(t.Id, t.PropertyId, LedgerDiscrepancy.MissingLedgerEffect,
                        "Status Returned but no Reversal ledger entry exists"));
                else if (reversals > 1)
                    findings.Add(new(t.Id, t.PropertyId, LedgerDiscrepancy.DuplicateLedgerEffect,
                        $"Status Returned with {reversals} Reversal ledger entries"));
            }

            if (t.CumulativeRefundedAmount != Sum(LedgerEntryType.Refund))
                findings.Add(new(t.Id, t.PropertyId, LedgerDiscrepancy.RefundSumMismatch,
                    "CumulativeRefundedAmount disagrees with the summed Refund ledger entries"));
        }

        foreach (var f in findings)
        {
            logger.LogWarning(
                "Ledger inconsistency detected: {Discrepancy} for transaction {PaymentTransactionId} on property {PropertyId} — {Detail}",
                f.Discrepancy, f.PaymentTransactionId, f.PropertyId, f.Detail);
            SentrySdk.CaptureMessage(
                $"Ledger inconsistency: {f.Discrepancy} on transaction {f.PaymentTransactionId}",
                SentryLevel.Warning);
        }

        return findings;
    }
}

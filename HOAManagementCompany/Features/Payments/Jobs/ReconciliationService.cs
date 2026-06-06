using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Webhooks;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Features.Payments.Jobs;

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
}

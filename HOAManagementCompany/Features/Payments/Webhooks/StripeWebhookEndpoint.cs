using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace HOAManagementCompany.Features.Payments.Webhooks;

// <!-- REPOWISE:START domain=payments-webhooks -->
// Stripe webhook ingress: signature verification, inbox dedupe (ProcessedWebhookEvents), and
// dispatch into WebhookProcessor. Always acks 200 after persisting the event (FR-022a).
// <!-- REPOWISE:END -->

/// <summary>
/// POST /payments/webhooks/stripe — the single Stripe webhook sink (FR-032, FR-017). The raw body is
/// signature-verified, persisted to the durable inbox <em>before</em> the 2xx ack so nothing is lost
/// on crash/scale-to-zero, deduplicated by Stripe event id, and processed idempotently. A duplicate
/// delivery is acked without reprocessing; a processing failure is left <c>Received</c> for the
/// reconciliation sweep to retry. Anonymous: authenticity comes from the signature, not a session.
/// </summary>
public class StripeWebhookEndpoint(
    IStripeGateway gateway,
    WebhookProcessor processor,
    ApplicationDbContext db,
    OutboxDispatcher dispatcher,
    PaymentMetrics metrics,
    ILogger<StripeWebhookEndpoint> logger)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/payments/webhooks/stripe");
        AllowAnonymous();
        Description(x => x.WithName("StripeWebhook").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Request.EnableBuffering();
        using var reader = new StreamReader(HttpContext.Request.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync(ct);
        var signature = HttpContext.Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        Event evt;
        try
        {
            evt = gateway.ConstructEvent(json, signature);   // Verifies signature + tolerance (StripeException on failure).
        }
        catch (StripeException)
        {
            metrics.RecordWebhookProcessed("unknown", "invalid_signature");
            await SendAsync(null, StatusCodes.Status400BadRequest, ct);
            return;
        }

        // Dedupe: a already-processed event is acked without re-applying side effects (FR-017).
        var inbox = await db.WebhookEventInbox.FirstOrDefaultAsync(w => w.StripeEventId == evt.Id, ct);
        if (inbox is { Status: WebhookProcessingStatus.Processed })
        {
            metrics.RecordWebhookProcessed(evt.Type, "duplicate");
            await SendOkAsync(ct);
            return;
        }

        if (inbox is null)
        {
            inbox = new WebhookEventInbox
            {
                StripeEventId = evt.Id,
                EventType = evt.Type,
                Payload = json,
                Status = WebhookProcessingStatus.Received,
            };
            db.WebhookEventInbox.Add(inbox);
            await db.SaveChangesAsync(ct);   // Durable record before processing — survives a crash mid-handler.
        }

        inbox.Attempts++;
        var processed = false;
        try
        {
            await processor.ProcessAsync(evt, ct);
            inbox.Status = WebhookProcessingStatus.Processed;
            inbox.ProcessedAt = DateTimeOffset.UtcNow;
            processed = true;
        }
        catch (Exception ex)
        {
            // Leave Received for the reconciliation sweep to retry; still ack so Stripe doesn't hammer us.
            inbox.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
        }
        await db.SaveChangesAsync(ct);
        metrics.RecordWebhookProcessed(evt.Type, processed ? "processed" : "failed");

        // Promptly drain any alerts the handler enqueued (SC-006 ≤5 min); reconcile is the backstop.
        // Never let a delivery hiccup fail the 2xx ack — Stripe would otherwise redeliver.
        if (processed)
        {
            try
            {
                await dispatcher.DispatchPendingAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Prompt outbox dispatch failed after webhook {EventId}; reconcile will retry", evt.Id);
            }
        }

        await SendOkAsync(ct);
    }
}

using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace HOAManagementCompany.Features.Payments.Webhooks;

/// <summary>
/// POST /payments/webhooks/stripe — the single Stripe webhook sink (FR-032, FR-017). The raw body is
/// signature-verified, persisted to the durable inbox <em>before</em> the 2xx ack so nothing is lost
/// on crash/scale-to-zero, deduplicated by Stripe event id, and processed idempotently. A duplicate
/// delivery is acked without reprocessing; a processing failure is left <c>Received</c> for the
/// reconciliation sweep to retry. Anonymous: authenticity comes from the signature, not a session.
/// </summary>
public class StripeWebhookEndpoint(IStripeGateway gateway, WebhookProcessor processor, ApplicationDbContext db)
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
            await SendAsync(null, StatusCodes.Status400BadRequest, ct);
            return;
        }

        // Dedupe: a already-processed event is acked without re-applying side effects (FR-017).
        var inbox = await db.WebhookEventInbox.FirstOrDefaultAsync(w => w.StripeEventId == evt.Id, ct);
        if (inbox is { Status: WebhookProcessingStatus.Processed })
        {
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
        try
        {
            await processor.ProcessAsync(evt, ct);
            inbox.Status = WebhookProcessingStatus.Processed;
            inbox.ProcessedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // Leave Received for the reconciliation sweep to retry; still ack so Stripe doesn't hammer us.
            inbox.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
        }
        await db.SaveChangesAsync(ct);

        await SendOkAsync(ct);
    }
}

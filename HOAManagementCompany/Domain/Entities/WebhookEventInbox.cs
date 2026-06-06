using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Durable webhook intake + idempotency + dead-letter log (FR-032, FR-017). A verified event is
/// persisted before the 2xx ack so nothing is lost on crash/scale-to-zero.
/// </summary>
public class WebhookEventInbox
{
    public Guid Id { get; set; }

    /// <summary>Stripe <c>evt_…</c> id; unique (dedupe key).</summary>
    public string StripeEventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    /// <summary>Verified raw event JSON, PII-scrubbed.</summary>
    public string Payload { get; set; } = string.Empty;

    public WebhookProcessingStatus Status { get; set; } = WebhookProcessingStatus.Received;
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

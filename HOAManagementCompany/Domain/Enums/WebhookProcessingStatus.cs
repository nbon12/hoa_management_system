namespace HOAManagementCompany.Domain.Enums;

/// <summary>Durable webhook intake state (FR-032, FR-017).</summary>
public enum WebhookProcessingStatus
{
    Received,
    Processed,
    DeadLettered
}

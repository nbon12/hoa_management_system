namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Immutable NACHA/dispute-defense record of a recurring authorization (FR-011b). Append-only
/// (only <see cref="TerminatedAt"/> is ever set); retained ≥ 2 years past termination.
/// </summary>
public class PaymentAuthorization
{
    public Guid Id { get; set; }
    public Guid RecurringPaymentId { get; set; }

    public string MandateText { get; set; } = string.Empty;
    public string MandateVersion { get; set; } = string.Empty;
    public string AmountTermsSnapshot { get; set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AcceptedIp { get; set; }
    public string? AcceptedUserAgent { get; set; }
    public string? StripeMandateId { get; set; }
    public DateTimeOffset? TerminatedAt { get; set; }

    public RecurringPayment RecurringPayment { get; set; } = null!;
}

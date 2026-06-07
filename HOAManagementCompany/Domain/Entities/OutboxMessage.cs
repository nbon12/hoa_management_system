using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Transactional outbox for alerts and receipts so a crash neither loses nor duplicates them
/// (FR-034). Written in the same DB transaction as the status change that triggers it.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// <c>sms_alert</c>, <c>email_alert</c>, <c>receipt_email</c>, or the variable-amount advance
    /// notices <c>variable_notice_sms</c> / <c>variable_notice_email</c> (FR-011c).
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }
    public Guid? TransactionId { get; set; }

    /// <summary>
    /// Deterministic per-message dedup token (filtered-unique) so a re-run of a producing job
    /// cannot enqueue the same notice twice (FR-011c/FR-011d). Null for one-off rows that carry
    /// no natural period key (failure alerts, receipts).
    /// </summary>
    public string? DedupKey { get; set; }

    /// <summary>Render inputs (no PII beyond the delivery target).</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}

using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// OutboxMessage: transactional outbox for alerts, receipts and architectural review
// emails. Each row targets exactly one recipient: an Owner (payments, ARC owner
// outcome emails) or an ApplicationUser (ARC board reminders and lapse notices, 027 R5).
// <!-- REPOWISE:END -->

/// <summary>
/// Transactional outbox for alerts and receipts so a crash neither loses nor duplicates them
/// (FR-034). Written in the same DB transaction as the status change that triggers it.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// <c>sms_alert</c>, <c>email_alert</c>, <c>receipt_email</c>, the variable-amount advance
    /// notices <c>variable_notice_sms</c> / <c>variable_notice_email</c> (FR-011c), or the
    /// architectural review emails <c>arc_owner_approved</c>, <c>arc_owner_revisions_requested</c>,
    /// <c>arc_owner_denied</c>, <c>arc_board_reminder</c>, <c>arc_board_lapsed</c> (027).
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Owner recipient. Exactly one of <see cref="OwnerId"/> / <see cref="RecipientUserId"/> is set.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>User recipient (board members, 027). Exactly one of the two recipients is set.</summary>
    public string? RecipientUserId { get; set; }
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

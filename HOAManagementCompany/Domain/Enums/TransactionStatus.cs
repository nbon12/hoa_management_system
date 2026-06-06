namespace HOAManagementCompany.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.PaymentTransaction"/> (FR-012).
/// Persisted as a string via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum TransactionStatus
{
    /// <summary>ACH submitted; awaiting settlement.</summary>
    Pending,
    Succeeded,
    Failed,
    /// <summary>Cumulative refund &lt; total (FR-014b).</summary>
    PartiallyRefunded,
    /// <summary>Cumulative refund == total.</summary>
    Refunded,
    /// <summary>Dispute created (FR-014).</summary>
    Disputed,
    /// <summary>Dispute closed-lost; chargeback stands (FR-014d).</summary>
    DisputeLost,
    /// <summary>Settled ACH later returned (FR-014c).</summary>
    Returned
}

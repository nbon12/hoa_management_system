using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Immutable audit record for every payment attempt — exactly one per charge/setup outcome
/// (SC-003). Distinct from the accounting <see cref="LedgerEntry"/>; referenced by the ledger
/// entries it produces (FR-007e, FR-012).
/// </summary>
public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid OwnerId { get; set; }

    public string? StripePaymentIntentId { get; set; }
    public string? StripeChargeId { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal Total { get; set; }
    public decimal CumulativeRefundedAmount { get; set; }
    public string Currency { get; set; } = HOAManagementCompany.Domain.Payments.MoneyPolicy.Currency;

    public TransactionStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public CardFunding? CardFunding { get; set; }

    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? ReturnCode { get; set; }

    public bool IsRecurring { get; set; }
    public string? IdempotencyKey { get; set; }

    // Settlement reconciliation references (FR-037).
    public string? StripeBalanceTransactionId { get; set; }
    public decimal? ProcessorFeeAmount { get; set; }
    public string? StripePayoutId { get; set; }

    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
    public Owner Owner { get; set; } = null!;
    public Receipt? Receipt { get; set; }
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
}

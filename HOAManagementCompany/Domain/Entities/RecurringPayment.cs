using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class RecurringPayment
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public RecurringAmountType AmountType { get; set; }
    public decimal? FixedAmount { get; set; }
    public PaymentMethod Method { get; set; }
    public int DraftDay { get; set; }
    public string Status { get; set; } = "active";
    public decimal ProcessingFee { get; set; }

    // Vaulted method-on-file (FR-009) — populated by the US2 SetupIntent flow. No raw PAN/bank
    // number is ever stored (SC-001/SC-008); only the Stripe reference and masked display detail.
    /// <summary>Stripe <c>pm_…</c> vaulted payment method.</summary>
    public string? VaultedPaymentMethodId { get; set; }

    /// <summary>Card brand or bank name for display (e.g. "visa").</summary>
    public string? MethodBrand { get; set; }

    /// <summary>Last four digits of the vaulted card/bank account for display.</summary>
    public string? MethodLast4 { get; set; }

    /// <summary>Funding type of the vaulted card, used for accurate surcharge calculation (FR-004b).</summary>
    public CardFunding? MethodFunding { get; set; }

    /// <summary>FK → the active immutable mandate authorization (FR-011b).</summary>
    public Guid? CurrentAuthorizationId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
    public ICollection<DraftEntry> DraftEntries { get; set; } = [];
    public ICollection<PaymentAuthorization> Authorizations { get; set; } = [];
}

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

    // ACH fields (masked) — DEPRECATED: removed when recurring moves to vaulted PMs (US2, FR-009/SC-008).
    public string? RoutingNumberMasked { get; set; }
    public string? AccountNumberMasked { get; set; }
    public string? AccountType { get; set; }

    // Card fields (masked) — DEPRECATED (see above).
    public string? CardNumberMasked { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardholderName { get; set; }
    public string? BillingZip { get; set; }

    // Vaulted method-on-file (FR-009) — populated by the US2 SetupIntent flow.
    /// <summary>Stripe <c>pm_…</c> vaulted payment method.</summary>
    public string? VaultedPaymentMethodId { get; set; }

    /// <summary>FK → the active immutable mandate authorization (FR-011b).</summary>
    public Guid? CurrentAuthorizationId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
    public ICollection<DraftEntry> DraftEntries { get; set; } = [];
    public ICollection<PaymentAuthorization> Authorizations { get; set; } = [];
}

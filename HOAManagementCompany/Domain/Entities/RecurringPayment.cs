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

    // ACH fields (masked)
    public string? RoutingNumberMasked { get; set; }
    public string? AccountNumberMasked { get; set; }
    public string? AccountType { get; set; }

    // Card fields (masked)
    public string? CardNumberMasked { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardholderName { get; set; }
    public string? BillingZip { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
    public ICollection<DraftEntry> DraftEntries { get; set; } = [];
}

using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class DraftEntry
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public DateOnly DraftDate { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DraftStatus Status { get; set; }

    /// <summary>FK → the <see cref="PaymentTransaction"/> for this draft (FR-010b); nullable, no cascade.</summary>
    public Guid? TransactionId { get; set; }

    public Property Property { get; set; } = null!;
    public PaymentTransaction? Transaction { get; set; }
}

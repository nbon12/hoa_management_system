using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string? DocumentNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal ChargeAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
}

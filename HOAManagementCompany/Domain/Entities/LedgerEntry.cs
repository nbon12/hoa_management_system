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

    /// <summary>Precise UTC timestamp (existing <see cref="EntryDate"/> is a <see cref="DateOnly"/>).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Append-only deterministic ordering (006-stripe-payments).
    /// <summary>Monotonic per-property order key — the authoritative balance-recompute order (FR-007d).</summary>
    public long Sequence { get; set; }

    /// <summary>FK → the <see cref="PaymentTransaction"/> that produced this entry, if any (FR-007e).</summary>
    public Guid? TransactionId { get; set; }

    /// <summary>Optional operating/reserve/fee-income GL hook (FR-038).</summary>
    public string? FundCode { get; set; }

    public Property Property { get; set; } = null!;
    public PaymentTransaction? Transaction { get; set; }
}

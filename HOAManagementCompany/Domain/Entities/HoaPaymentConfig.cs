using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Per-HOA payment policy so fees/allocation/NSF/jurisdiction are configuration, not code
/// (FR-004b, FR-007b, FR-014e). Late-fee/finance-charge knobs live on <see cref="Property"/>.
/// </summary>
public class HoaPaymentConfig
{
    public Guid Id { get; set; }

    /// <summary>Tenant key — the community (HOA) this policy applies to (matches <see cref="Property.CommunityId"/>).</summary>
    public string CommunityId { get; set; } = string.Empty;

    public FeeType CardFeeType { get; set; } = FeeType.Percentage;

    /// <summary>Flat amount or rate (e.g. 0.03).</summary>
    public decimal CardFeeValue { get; set; }

    public CardScope CardScope { get; set; } = CardScope.CreditOnly;

    /// <summary>Per-jurisdiction surcharge gate (default off where restricted).</summary>
    public bool SurchargingEnabled { get; set; }

    public decimal AchFeeValue { get; set; }

    /// <summary>Ordered category priority for payment allocation (FR-007b), JSON array.</summary>
    public string AllocationOrderJson { get; set; } = "[\"RegularAssessment\",\"LateFee\",\"FinanceCharge\"]";

    public bool NsfFeeEnabled { get; set; }
    public decimal NsfFeeAmount { get; set; }

    /// <summary>NACHA variable-amount notice lead (FR-011c).</summary>
    public int VariableNoticeLeadDays { get; set; } = 10;
}

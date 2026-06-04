namespace HOAManagementCompany.Domain.Entities;

public class Property
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string CommunityId { get; set; } = string.Empty;
    public string CommunityName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public string Lot { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public string Section { get; set; } = string.Empty;
    public string? Block { get; set; }
    public int FiscalYear { get; set; }
    public int YearBuilt { get; set; }
    public string Status { get; set; } = "active";
    public decimal MonthlyAssessment { get; set; }
    public decimal AnnualAssessment { get; set; }
    public int AssessmentDueDay { get; set; }
    public decimal LateFeeAmount { get; set; }
    public int LateFeeGraceDays { get; set; }
    public decimal FinanceChargeRate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Owner? Owner { get; set; }
    public ICollection<UserProperty> UserProperties { get; set; } = [];
    public ICollection<AddressHistory> AddressHistories { get; set; } = [];
    public ICollection<DirectoryField> DirectoryFields { get; set; } = [];
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
    public ICollection<RecurringPayment> RecurringPayments { get; set; } = [];
    public ICollection<DraftEntry> DraftEntries { get; set; } = [];
    public ICollection<Violation> Violations { get; set; } = [];
}

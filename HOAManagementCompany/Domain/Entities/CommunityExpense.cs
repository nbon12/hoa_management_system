namespace HOAManagementCompany.Domain.Entities;

public class CommunityExpense
{
    public Guid Id { get; set; }
    public string CommunityId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int FiscalYear { get; set; }
}

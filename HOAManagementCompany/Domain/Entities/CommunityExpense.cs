namespace HOAManagementCompany.Domain.Entities;

public class CommunityExpense
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int FiscalYear { get; set; }
}

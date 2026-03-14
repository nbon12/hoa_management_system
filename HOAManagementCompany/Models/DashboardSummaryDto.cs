namespace HOAManagementCompany.Models;

public class DashboardSummaryDto
{
    public int OpenViolationCount { get; set; }
    public decimal? CurrentBalance { get; set; }
    public int? WorkOrdersCount { get; set; }
    public int? ArchitectureRequestsCount { get; set; }
}

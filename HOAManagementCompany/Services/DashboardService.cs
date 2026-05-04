using HOAManagementCompany.Models;

namespace HOAManagementCompany.Services;

public class DashboardService
{
    private readonly ViolationService _violationService;

    public DashboardService(ViolationService violationService)
    {
        _violationService = violationService;
    }

    /// <summary>
    /// Returns dashboard summary for the current user. Only openViolationCount is real; other boxes are placeholders (null).
    /// </summary>
    public async Task<DashboardSummaryDto> GetSummaryAsync(string? userId)
    {
        var openViolationCount = string.IsNullOrEmpty(userId)
            ? 0
            : await _violationService.GetOpenViolationCountForUserAsync(userId);

        return new DashboardSummaryDto
        {
            OpenViolationCount = openViolationCount,
            CurrentBalance = null,
            WorkOrdersCount = null,
            ArchitectureRequestsCount = null
        };
    }
}

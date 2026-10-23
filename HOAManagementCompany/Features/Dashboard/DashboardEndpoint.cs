using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Dashboard.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Dashboard;

public class DashboardEndpoint(ResidentAccess access, DashboardService dashboardService) : EndpointWithoutRequest<DashboardResponse>
{
    public override void Configure()
    {
        Get("/dashboard");
        Description(x => x.WithName("GetDashboard").WithTags("Dashboard"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var propertyId = home.PropertyId;
        var communityId = home.CommunityId;
        var result = await dashboardService.GetDashboardAsync(propertyId, communityId, ct);
        await SendOkAsync(result, ct);
    }
}

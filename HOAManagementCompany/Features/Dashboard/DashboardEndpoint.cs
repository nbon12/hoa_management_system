using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Dashboard.Models;

namespace HOAManagementCompany.Features.Dashboard;

public class DashboardEndpoint(DashboardService dashboardService) : EndpointWithoutRequest<DashboardResponse>
{
    public override void Configure()
    {
        Get("/dashboard");
        Description(x => x.WithName("GetDashboard").WithTags("Dashboard"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.RequirePropertyId();
        var communityId = User.RequireCommunityId();

        var result = await dashboardService.GetDashboardAsync(propertyId, communityId, ct);
        await SendOkAsync(result, ct);
    }
}

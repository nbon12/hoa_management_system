using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Violations;

public class ViolationsEndpoint(ResidentAccess access, CommunityService communityService) : Endpoint<ViolationListRequest, ViolationListResponse>
{
    public override void Configure()
    {
        Get("/community/violations");
        Description(x => x.WithName("GetViolations").WithTags("Community"));
    }

    public override async Task HandleAsync(ViolationListRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var propertyId = home.PropertyId;
        await SendOkAsync(await communityService.GetViolationsAsync(propertyId, req, ct), ct);
    }
}

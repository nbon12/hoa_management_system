using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Violations;

public class ViolationsEndpoint(CommunityService communityService) : Endpoint<ViolationListRequest, ViolationListResponse>
{
    public override void Configure()
    {
        Get("/community/violations");
        Description(x => x.WithName("GetViolations").WithTags("Community"));
    }

    public override async Task HandleAsync(ViolationListRequest req, CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        await SendOkAsync(await communityService.GetViolationsAsync(propertyId, req, ct), ct);
    }
}

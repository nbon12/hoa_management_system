using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Directory;

public class CommunityDirectoryEndpoint(ResidentAccess access, CommunityService communityService) : EndpointWithoutRequest<CommunityDirectoryResponse>
{
    public override void Configure()
    {
        Get("/community/directory");
        Description(x => x.WithName("GetCommunityDirectory").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var communityId = home.CommunityId;
        var propertyId = home.PropertyId;
        await SendOkAsync(await communityService.GetCommunityDirectoryAsync(communityId, propertyId, ct), ct);
    }
}

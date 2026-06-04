using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Directory;

public class CommunityDirectoryEndpoint(CommunityService communityService) : EndpointWithoutRequest<CommunityDirectoryResponse>
{
    public override void Configure()
    {
        Get("/community/directory");
        Description(x => x.WithName("GetCommunityDirectory").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var communityId = User.FindFirst("communityId")!.Value;
        var propertyId  = Guid.Parse(User.FindFirst("propertyId")!.Value);
        await SendOkAsync(await communityService.GetCommunityDirectoryAsync(communityId, propertyId, ct), ct);
    }
}

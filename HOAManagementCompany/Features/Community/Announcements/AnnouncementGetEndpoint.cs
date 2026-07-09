using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Announcements;

public class AnnouncementGetEndpoint(CommunityService communityService) : EndpointWithoutRequest<AnnouncementDto>
{
    public override void Configure()
    {
        Get("/community/announcements/{id}");
        Description(x => x.WithName("GetAnnouncement").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        var id = Route<Guid>("id");
        await SendOkAsync(await communityService.GetAnnouncementAsync(communityId, id, ct), ct);
    }
}

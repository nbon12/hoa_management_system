using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Announcements;

public class AnnouncementsListEndpoint(CommunityService communityService) : Endpoint<AnnouncementListRequest, AnnouncementListResponse>
{
    public override void Configure()
    {
        Get("/community/announcements");
        Description(x => x.WithName("GetAnnouncements").WithTags("Community"));
    }

    public override async Task HandleAsync(AnnouncementListRequest req, CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        await SendOkAsync(await communityService.GetAnnouncementsAsync(communityId, req, ct), ct);
    }
}

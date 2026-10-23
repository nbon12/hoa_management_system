using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Announcements;

public class AnnouncementsListEndpoint(ResidentAccess access, CommunityService communityService) : Endpoint<AnnouncementListRequest, AnnouncementListResponse>
{
    public override void Configure()
    {
        Get("/community/announcements");
        Description(x => x.WithName("GetAnnouncements").WithTags("Community"));
    }

    public override async Task HandleAsync(AnnouncementListRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var communityId = home.CommunityId;
        await SendOkAsync(await communityService.GetAnnouncementsAsync(communityId, req, ct), ct);
    }
}

using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Events;

public class EventsEndpoint(ResidentAccess access, CommunityService communityService) : Endpoint<EventListRequest, EventListResponse>
{
    public override void Configure()
    {
        Get("/community/events");
        Description(x => x.WithName("GetEvents").WithTags("Community"));
    }

    public override async Task HandleAsync(EventListRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var communityId = home.CommunityId;
        await SendOkAsync(await communityService.GetEventsAsync(communityId, req, ct), ct);
    }
}

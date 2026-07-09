using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Events;

public class EventsEndpoint(CommunityService communityService) : Endpoint<EventListRequest, EventListResponse>
{
    public override void Configure()
    {
        Get("/community/events");
        Description(x => x.WithName("GetEvents").WithTags("Community"));
    }

    public override async Task HandleAsync(EventListRequest req, CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        await SendOkAsync(await communityService.GetEventsAsync(communityId, req, ct), ct);
    }
}

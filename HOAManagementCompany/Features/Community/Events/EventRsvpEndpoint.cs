using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Events;

public class EventRsvpEndpoint(CommunityService communityService) : Endpoint<EventRsvpRequest>
{
    public override void Configure()
    {
        Post("/community/events/{id}/rsvp");
        Description(x => x.WithName("RsvpEvent").WithTags("Community"));
    }

    public override async Task HandleAsync(EventRsvpRequest req, CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        var userId = User.GetUserId();
        var eventId = Route<Guid>("id");

        await communityService.RsvpEventAsync(communityId, eventId, userId, req.Attending, ct);
        await SendNoContentAsync(ct);
    }
}

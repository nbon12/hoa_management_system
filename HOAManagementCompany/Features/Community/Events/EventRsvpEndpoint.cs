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
        var communityId = User.FindFirst("communityId")!.Value;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var eventId = Route<Guid>("id");

        try
        {
            await communityService.RsvpEventAsync(communityId, eventId, userId, req.Attending, ct);
            await SendNoContentAsync(ct);
        }
        catch (DomainException ex)
        {
            await SendAsync(new { code = ex.Code, message = ex.Message }, ex.StatusCode, ct);
        }
    }
}

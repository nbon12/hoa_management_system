using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Events;

public class EventRsvpEndpoint(ResidentAccess access, CommunityService communityService) : Endpoint<EventRsvpRequest>
{
    public override void Configure()
    {
        Post("/community/events/{id}/rsvp");
        Description(x => x.WithName("RsvpEvent").WithTags("Community"));
    }

    public override async Task HandleAsync(EventRsvpRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Write, ct);
        var communityId = home.CommunityId;
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

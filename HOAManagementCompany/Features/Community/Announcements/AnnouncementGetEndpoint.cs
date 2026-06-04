using FastEndpoints;
using HOAManagementCompany.Features.Auth;
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
        var communityId = User.FindFirst("communityId")!.Value;
        var id = Route<Guid>("id");
        try { await SendOkAsync(await communityService.GetAnnouncementAsync(communityId, id, ct), ct); }
        catch (DomainException ex) { HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct); }
    }
}

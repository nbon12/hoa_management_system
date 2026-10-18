using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Poll;

public class PollGetEndpoint(ResidentAccess access, PollService pollService) : EndpointWithoutRequest<PollDto>
{
    public override void Configure()
    {
        Get("/community/poll");
        Description(x => x.WithName("GetPoll").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var communityId = home.CommunityId;
        var poll = await pollService.GetActivePollAsync(communityId, ct);
        if (poll is null)
            await SendNoContentAsync(ct);
        else
            await SendOkAsync(poll, ct);
    }
}

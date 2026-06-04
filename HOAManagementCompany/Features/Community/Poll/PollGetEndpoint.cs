using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Poll;

public class PollGetEndpoint(PollService pollService) : EndpointWithoutRequest<PollDto>
{
    public override void Configure()
    {
        Get("/community/poll");
        Description(x => x.WithName("GetPoll").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var communityId = User.FindFirst("communityId")!.Value;
        var poll = await pollService.GetActivePollAsync(communityId, ct);
        if (poll is null)
            await SendNoContentAsync(ct);
        else
            await SendOkAsync(poll, ct);
    }
}

using HOAManagementCompany.Features.Common;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Poll;

public class PollVoteEndpoint(PollService pollService) : Endpoint<PollVoteRequest, PollDto>
{
    public override void Configure()
    {
        Post("/community/poll/{id}/vote");
        Description(x => x.WithName("VotePoll").WithTags("Community"));
    }

    public override async Task HandleAsync(PollVoteRequest req, CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        var userId = User.GetUserId();
        var pollId = Route<Guid>("id");

        var result = await pollService.VoteAsync(communityId, pollId, userId, req.OptionIndex, ct);
        await SendOkAsync(result, ct);
    }
}

public class PollVoteValidator : Validator<PollVoteRequest>
{
    public PollVoteValidator()
    {
        RuleFor(x => x.OptionIndex).GreaterThanOrEqualTo(0);
    }
}

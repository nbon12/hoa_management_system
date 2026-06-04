using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Auth;
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
        var communityId = User.FindFirst("communityId")!.Value;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var pollId = Route<Guid>("id");

        try
        {
            var result = await pollService.VoteAsync(communityId, pollId, userId, req.OptionIndex, ct);
            await SendOkAsync(result, ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

public class PollVoteValidator : Validator<PollVoteRequest>
{
    public PollVoteValidator()
    {
        RuleFor(x => x.OptionIndex).GreaterThanOrEqualTo(0);
    }
}

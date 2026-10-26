using FastEndpoints;
using HOAManagementCompany.Features.Common;
using FluentValidation;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Poll;

public class PollVoteEndpoint(ResidentAccess access, PollService pollService) : Endpoint<PollVoteRequest, PollDto>
{
    public override void Configure()
    {
        Post("/community/poll/{id}/vote");
        Description(x => x.WithName("VotePoll").WithTags("Community"));
    }

    public override async Task HandleAsync(PollVoteRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Write, ct);
        var communityId = home.CommunityId;
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

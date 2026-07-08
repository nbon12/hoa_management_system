using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Community;

public class PollService(ApplicationDbContext db)
{
    public async Task<PollDto?> GetActivePollAsync(string communityId, CancellationToken ct = default)
    {
        var poll = await db.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.CommunityId == communityId && p.IsActive, ct);

        return poll is null ? null : MapPoll(poll);
    }

    public async Task<PollDto> VoteAsync(string communityId, Guid pollId, string userId, int optionIndex, CancellationToken ct = default)
    {
        var poll = await db.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.CommunityId == communityId && p.Id == pollId && p.IsActive, ct)
            ?? throw new DomainException("NOT_FOUND", "Active poll not found.", 404);

        if (!poll.Options.Any(o => o.OptionIndex == optionIndex))
            throw new DomainException("INVALID_OPTION", "Invalid option index.", 422);

        if (await db.PollVotes.AnyAsync(v => v.PollId == pollId && v.UserId == userId, ct))
            throw new DomainException("ALREADY_VOTED", "You have already voted in this poll.", 409);

        db.PollVotes.Add(new PollVote { PollId = pollId, UserId = userId, OptionIndex = optionIndex });

        var option = poll.Options.First(o => o.OptionIndex == optionIndex);
        option.VoteCount++;
        poll.TotalVotes++;

        foreach (var opt in poll.Options)
            opt.Percentage = poll.TotalVotes > 0 ? Math.Round((decimal)opt.VoteCount / poll.TotalVotes * 100, 2) : 0m;

        await db.SaveChangesAsync(ct);
        return MapPoll(poll);
    }

    private static PollDto MapPoll(Domain.Entities.Poll poll) => new(
        poll.Id,
        poll.Question,
        poll.ClosingLabel,
        poll.TotalVotes,
        poll.Options.OrderBy(o => o.OptionIndex).Select(o => new PollOptionDto(o.OptionIndex, o.OptionText, o.VoteCount, o.Percentage)));
}

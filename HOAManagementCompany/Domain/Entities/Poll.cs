namespace HOAManagementCompany.Domain.Entities;

public class Poll
{
    public Guid Id { get; set; }
    public string CommunityId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string ClosingLabel { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PollOption> Options { get; set; } = [];
    public ICollection<PollVote> Votes { get; set; } = [];
}

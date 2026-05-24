namespace HOAManagementCompany.Domain.Entities;

public class PollVote
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int OptionIndex { get; set; }
    public DateTimeOffset VotedAt { get; set; } = DateTimeOffset.UtcNow;

    public Poll Poll { get; set; } = null!;
}

namespace HOAManagementCompany.Domain.Entities;

public class PollOption
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public int OptionIndex { get; set; }
    public int VoteCount { get; set; }
    public decimal Percentage { get; set; }

    public Poll Poll { get; set; } = null!;
}

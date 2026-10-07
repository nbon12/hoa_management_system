using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// One board member's vote on one application revision (027 FR-015/FR-016).
// Immutable once cast; unique per (ApplicationId, VoterUserId).
public class ArchitecturalVote
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string VoterUserId { get; set; } = string.Empty;
    public ArcVoteChoice Choice { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CastAt { get; set; } = DateTimeOffset.UtcNow;

    public ArchitecturalApplication Application { get; set; } = null!;
    public ApplicationUser Voter { get; set; } = null!;
}

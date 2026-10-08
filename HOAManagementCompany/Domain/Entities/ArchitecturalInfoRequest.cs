namespace HOAManagementCompany.Domain.Entities;

// A board member's request for more information (027 FR-021). Not a vote and never
// moves the due date. RespondedAt is set by the resident submission spec.
public class ArchitecturalInfoRequest
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }

    // The resident's reply (029 FR-018), visible to the board and manager.
    public string? ResponseMessage { get; set; }
    public string? RespondedByUserId { get; set; }

    public ArchitecturalApplication Application { get; set; } = null!;
    public ApplicationUser RequestedBy { get; set; } = null!;
}

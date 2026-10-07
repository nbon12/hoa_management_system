using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// ArchitecturalApplication: one row per revision of a homeowner's architectural
// request (027 data-model.md). ApplicationNumber (shown ARC-<n>) is shared by all
// revisions; Revision 2+ links to the previous row and is shown with a "v<n>" badge.
// The decision rule, lapse rule and time zone are snapshotted at submission (FR-030).
// Shared with the Resident Architectural Application Submission spec.
// <!-- REPOWISE:END -->

public class ArchitecturalApplication
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Guid PropertyId { get; set; }
    public int ApplicationNumber { get; set; }
    public int Revision { get; set; } = 1;
    public Guid? PreviousRevisionId { get; set; }
    public string? SubmittedByUserId { get; set; }

    public string OwnerName { get; set; } = string.Empty;
    public ArcProjectType ProjectType { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public DateOnly ReceivedDate { get; set; }
    public DateOnly DueDate { get; set; }
    public ArcDecisionRule DecisionRule { get; set; }
    public ArcLapseRule LapseRule { get; set; }
    public string TimeZoneId { get; set; } = CommunityArcSettings.DefaultTimeZoneId;

    public ArcApplicationStatus Status { get; set; } = ArcApplicationStatus.Open;
    public ArcOutcome? DecisionOutcome { get; set; }
    public ArcDenialWording? DecisionWording { get; set; }
    public ArcDecisionSource? DecisionSource { get; set; }
    public DateTimeOffset? DecisionReachedAt { get; set; }

    public string? ConditionsOfApproval { get; set; }
    public string? OwnerReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedByUserId { get; set; }

    // Sweep idempotency stamps (027 research R6).
    public DateTimeOffset? ReminderSentAt { get; set; }
    public DateTimeOffset? LapseProcessedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Community Community { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public ArchitecturalApplication? PreviousRevision { get; set; }
    public ICollection<ArchitecturalAttachment> Attachments { get; set; } = [];
    public ICollection<ArchitecturalVote> Votes { get; set; } = [];
    public ICollection<ArchitecturalInfoRequest> InfoRequests { get; set; } = [];

    public string DisplayId => $"ARC-{ApplicationNumber}";
}

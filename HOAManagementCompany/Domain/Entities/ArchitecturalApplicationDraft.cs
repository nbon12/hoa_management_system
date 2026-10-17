using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// ArchitecturalApplicationDraft: a resident's unsubmitted architectural request (029 research R3).
// Drafts never live in ArchitecturalApplications (027's number and dates are always set), so the
// board never sees them. Submitting feeds the draft into ArcApplicationFactory and deletes it.
// A draft with PreviousRevisionId is a revise-and-resubmit of a closed, denied revision.
// <!-- REPOWISE:END -->

public class ArchitecturalApplicationDraft
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Guid PropertyId { get; set; }
    public string? CreatedByUserId { get; set; }

    /// <summary>Set for a revise-and-resubmit draft; at most one draft per previous revision.</summary>
    public Guid? PreviousRevisionId { get; set; }

    public ArcProjectType ProjectType { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedCompletionDate { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorContact { get; set; }

    /// <summary>"Work may not begin until approved" (FR-003); required to submit.</summary>
    public bool Acknowledged { get; set; }

    /// <summary>Previous-revision attachment IDs the resident removed from a revision draft.</summary>
    public List<Guid> RemovedCarriedAttachmentIds { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
    public ArchitecturalApplication? PreviousRevision { get; set; }
    public ICollection<ArchitecturalDraftAttachment> Attachments { get; set; } = [];
}

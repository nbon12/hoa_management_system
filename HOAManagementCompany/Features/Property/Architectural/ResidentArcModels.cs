using HOAManagementCompany.Features.Board.Architectural;

namespace HOAManagementCompany.Features.Property.Architectural;

/// <summary>Resident ARC error codes (029 contract). Shared codes come from <see cref="ArcErrorCodes"/>.</summary>
public static class ResidentArcErrorCodes
{
    public const string Forbidden = ArcErrorCodes.Forbidden;
    public const string ValidationError = ArcErrorCodes.ValidationError;
    public const string ApplicationDecided = ArcErrorCodes.ApplicationDecided;
    public const string ApplicationClosed = ArcErrorCodes.ApplicationClosed;
    public const string AttachmentUnavailable = ArcErrorCodes.AttachmentUnavailable;
    public const string NotFound = "NOT_FOUND";
    public const string AcknowledgementRequired = "ACKNOWLEDGEMENT_REQUIRED";
    public const string UnsupportedFileType = "UNSUPPORTED_FILE_TYPE";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string AttachmentLimitReached = "ATTACHMENT_LIMIT_REACHED";
    public const string InfoAlreadyAnswered = "INFO_ALREADY_ANSWERED";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    /// <summary>Raised by <see cref="ArcApplicationFactory.CreateRevisionAsync"/> and the revise endpoint.</summary>
    public const string RevisionNotAllowed = "REVISION_NOT_ALLOWED";
}

/// <summary>Resident-facing status projection (029 data-model.md).</summary>
public static class ResidentArcStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string MoreInfoRequested = "MoreInfoRequested";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
    public const string Withdrawn = "Withdrawn";
}

// ── Requests ────────────────────────────────────────────────────────────────

/// <summary>Body of create/update draft. Only <see cref="ProjectType"/> is required while drafting.</summary>
public class ResidentArcDraftBody
{
    public string? ProjectType { get; set; }
    public string? ProjectTitle { get; set; }
    public string? Description { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedCompletionDate { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorContact { get; set; }
    public bool Acknowledged { get; set; }
    /// <summary>Revision drafts only: carried-over attachments the resident removed.</summary>
    public List<Guid>? RemovedCarriedAttachmentIds { get; set; }
}

public sealed class ResidentArcDraftUpdateRequest : ResidentArcDraftBody
{
    public Guid DraftId { get; set; }
}

public sealed class ResidentArcDraftRoute
{
    public Guid DraftId { get; set; }
}

public sealed class ResidentArcDraftAttachmentRoute
{
    public Guid DraftId { get; set; }
    public Guid AttachmentId { get; set; }
}

public sealed class ResidentArcDraftUploadRequest
{
    public Guid DraftId { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class ResidentArcRoute
{
    public Guid Id { get; set; }
}

public sealed class ResidentArcAttachmentRoute
{
    public Guid Id { get; set; }
    public Guid AttachmentId { get; set; }
}

public sealed class ResidentArcListQuery
{
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed class ResidentArcReplyUploadRequest
{
    public Guid Id { get; set; }
    public Guid InfoRequestId { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class ResidentArcReplyRequest
{
    public Guid Id { get; set; }
    public Guid InfoRequestId { get; set; }
    public string? ResponseMessage { get; set; }
}

// ── Responses (resident-safe: no vote, voter, tally or vote-comment field exists) ──

public sealed record ResidentArcAttachmentDto(Guid Id, string FileName, long SizeBytes, string ContentType, Guid? InfoRequestId);

public sealed record ResidentArcDraftDto(
    Guid Id,
    string ProjectType,
    string ProjectTitle,
    string Description,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedCompletionDate,
    string? ContractorName,
    string? ContractorContact,
    bool Acknowledged,
    Guid? PreviousRevisionId,
    string? PreviousDisplayId,
    IReadOnlyList<ResidentArcAttachmentDto> Attachments,
    IReadOnlyList<ResidentArcAttachmentDto> CarriedAttachments,
    IReadOnlyList<Guid> RemovedCarriedAttachmentIds,
    DateTimeOffset UpdatedAt);

public sealed record ResidentArcListItemDto(
    string Kind,
    Guid Id,
    string? DisplayId,
    int Revision,
    string ProjectType,
    string ProjectTitle,
    string Status,
    int AttachmentCount,
    DateOnly? ReceivedDate,
    DateOnly? DueDate,
    DateTimeOffset UpdatedAt);

public sealed record ResidentArcListResponse(IReadOnlyList<ResidentArcListItemDto> Items, int Total, int Limit, int Offset);

public sealed record ResidentArcInfoRequestDto(
    Guid Id, string Message, DateTimeOffset RequestedAt, string? ResponseMessage, DateTimeOffset? RespondedAt);

public sealed record ResidentArcTimelineEventDto(string Event, DateTimeOffset At);

public sealed record ResidentArcDecisionDto(string Outcome, string? Wording);

public sealed record ResidentArcRevisionDto(Guid Id, int Revision, string Status, DateOnly ReceivedDate);

public sealed record ResidentArcDetailDto(
    string Kind,
    Guid Id,
    string? DisplayId,
    int Revision,
    string ProjectType,
    string ProjectTitle,
    string Status,
    int AttachmentCount,
    DateOnly? ReceivedDate,
    DateOnly? DueDate,
    DateTimeOffset UpdatedAt,
    string Description,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedCompletionDate,
    string? ContractorName,
    string? ContractorContact,
    DateTimeOffset? AcknowledgedAt,
    IReadOnlyList<ResidentArcAttachmentDto> Attachments,
    IReadOnlyList<ResidentArcInfoRequestDto> InfoRequests,
    IReadOnlyList<ResidentArcTimelineEventDto> Timeline,
    ResidentArcDecisionDto? Decision,
    string? OwnerReason,
    string? FormalDisapprovalStatement,
    string? ConditionsOfApproval,
    DateTimeOffset? ClosedAt,
    bool CanWithdraw,
    bool CanRevise,
    IReadOnlyList<ResidentArcRevisionDto> Revisions);

public sealed record ResidentArcAttachmentUrlDto(string Url, DateTimeOffset ExpiresAt);

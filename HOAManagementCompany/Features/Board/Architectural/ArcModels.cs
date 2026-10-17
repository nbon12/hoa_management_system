namespace HOAManagementCompany.Features.Board.Architectural;

// Request/response DTOs for the architectural applications contract
// (specs/027-board-arc-review/contracts/architectural-applications.md).

public static class ArcErrorCodes
{
    public const string Forbidden = "FORBIDDEN";
    public const string Recused = "RECUSED";
    public const string AlreadyVoted = "ALREADY_VOTED";
    public const string ApplicationDecided = "APPLICATION_DECIDED";
    public const string ApplicationClosed = "APPLICATION_CLOSED";
    public const string NoDecision = "NO_DECISION";
    public const string EmailNotFailed = "EMAIL_NOT_FAILED";
    public const string AttachmentUnavailable = "ATTACHMENT_UNAVAILABLE";
    public const string ValidationError = "VALIDATION_ERROR";
}

public static class ArcVoteStates
{
    public const string CanVote = "CanVote";
    public const string Voted = "Voted";
    public const string Recused = "Recused";
    public const string NotEligible = "NotEligible";
}

public static class ArcOwnerEmailStatuses
{
    public const string NoOwnerEmail = "NoOwnerEmail";
}

// ── Requests ─────────────────────────────────────────────────────────────────

public sealed record ArcListQuery
{
    public Guid CommunityId { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public bool? AwaitingMyVote { get; init; }
    /// <summary>029: include resident-withdrawn applications in the closed tab (default false).</summary>
    public bool? IncludeWithdrawn { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}

public sealed record ArcApplicationRoute
{
    public Guid CommunityId { get; init; }
    public Guid ApplicationId { get; init; }
}

public sealed record ArcAttachmentRoute
{
    public Guid CommunityId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid AttachmentId { get; init; }
}

public sealed record ArcVoteRequest
{
    public Guid CommunityId { get; init; }
    public Guid ApplicationId { get; init; }
    public string? Choice { get; init; }
    public string? Comment { get; init; }
}

public sealed record ArcInfoRequestRequest
{
    public Guid CommunityId { get; init; }
    public Guid ApplicationId { get; init; }
    public string? Message { get; init; }
}

public sealed record ArcOutcomeRequest
{
    public Guid CommunityId { get; init; }
    public Guid ApplicationId { get; init; }
    public string? OwnerReason { get; init; }
    public string? ConditionsOfApproval { get; init; }
    public string? Wording { get; init; }
}

public sealed record ArcSettingsRoute
{
    public Guid CommunityId { get; init; }
}

public sealed record ArcSettingsPutRequest
{
    public Guid CommunityId { get; init; }
    public int ReviewPeriodDays { get; init; }
    public string? LapseRule { get; init; }
    public string? DecisionRule { get; init; }
    public int ReminderDays { get; init; }
    public string? TimeZoneId { get; init; }
    public string? FormalDisapprovalStatement { get; init; }
}

// ── Responses ────────────────────────────────────────────────────────────────

public sealed record ArcTallyDto(int Approve, int RevisionsNeeded, int Deny, int NotVoted, int Eligible);

public sealed record ArcMyVoteDto(string State, string? Choice = null);

public sealed record ArcDecisionDto(string Outcome, string? Wording, string? Source);

public sealed record ArcListItemDto(
    Guid Id,
    string DisplayId,
    int Revision,
    string PropertyAddress,
    string OwnerName,
    string ProjectTitle,
    int AttachmentCount,
    DateOnly ReceivedDate,
    DateOnly DueDate,
    bool Overdue,
    string Status,
    ArcDecisionDto? Decision,
    bool InfoRequested,
    ArcTallyDto Tally,
    ArcMyVoteDto MyVote);

public sealed record ArcCountsDto(int Open, int Closed, int AwaitingMyVote);

public sealed record ArcListResponse(
    IReadOnlyList<ArcListItemDto> Items, int Total, int Limit, int Offset, ArcCountsDto Counts);

// InfoRequestId is set for a file the owner added with an info-request reply (029).
public sealed record ArcAttachmentDto(Guid Id, string FileName, long SizeBytes, string ContentType, Guid? InfoRequestId = null);

public sealed record ArcVoteDto(string VoterName, string Choice, string? Comment, DateTimeOffset CastAt);

// ResponseMessage is the owner's reply (029 FR-018), visible to board members and managers.
public sealed record ArcInfoRequestDto(
    Guid Id, string RequestedBy, string Message, DateTimeOffset RequestedAt, DateTimeOffset? RespondedAt,
    string? ResponseMessage = null);

public sealed record ArcRevisionSummaryDto(Guid Id, int Revision, DateOnly ReceivedDate, ArcDecisionDto? Decision);

public sealed record ArcDetailDto(
    Guid Id,
    string DisplayId,
    int Revision,
    string PropertyAddress,
    string OwnerName,
    string ProjectTitle,
    string ProjectType,
    string Description,
    int AttachmentCount,
    DateOnly ReceivedDate,
    DateOnly DueDate,
    bool Overdue,
    string Status,
    ArcDecisionDto? Decision,
    bool InfoRequested,
    ArcTallyDto Tally,
    ArcMyVoteDto MyVote,
    IReadOnlyList<ArcAttachmentDto> Attachments,
    IReadOnlyList<ArcVoteDto> Votes,
    IReadOnlyList<ArcInfoRequestDto> InfoRequests,
    string RuleText,
    string? ConditionsOfApproval,
    string? OwnerReason,
    DateTimeOffset? ClosedAt,
    string? OwnerEmailStatus,
    IReadOnlyList<ArcRevisionSummaryDto> Revisions);

public sealed record ArcAttachmentUrlDto(string Url, DateTimeOffset ExpiresAt);

public sealed record ArcInfoRequestCreatedDto(
    Guid Id, string RequestedBy, string Message, DateTimeOffset RequestedAt, DateOnly DueDate);

public sealed record ArcResendDto(string? OwnerEmailStatus);

public sealed record ArcSettingsDto(
    int ReviewPeriodDays,
    string LapseRule,
    string DecisionRule,
    int ReminderDays,
    string TimeZoneId,
    string FormalDisapprovalStatement,
    DateTimeOffset? UpdatedAt);

public sealed record ArcSweepResultDto(
    int RemindersQueued, int LapsesProcessed, int DecisionsAtDueDate, int EmailsDispatched);

using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// CommunityArcSettings: per-community architectural review rules (027 FR-029/FR-030),
// set by the community manager to follow the association's governing documents.
// Applications snapshot the rule fields when received, so later edits never change
// an application already under review. NextApplicationNumber backs the ARC-<n> handle.
// <!-- REPOWISE:END -->

public class CommunityArcSettings
{
    public const int DefaultReviewPeriodDays = 30;
    public const int DefaultReminderDays = 7;
    public const string DefaultTimeZoneId = "America/New_York";
    public const int FirstApplicationNumber = 1001;
    public const string DefaultFormalDisapprovalStatement =
        "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit.";

    public Guid CommunityId { get; set; }
    public int ReviewPeriodDays { get; set; } = DefaultReviewPeriodDays;
    public ArcLapseRule LapseRule { get; set; } = ArcLapseRule.FlagOverdueOnly;
    public ArcDecisionRule DecisionRule { get; set; } = ArcDecisionRule.MajorityOfMembers;
    public int ReminderDays { get; set; } = DefaultReminderDays;
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;
    public string FormalDisapprovalStatement { get; set; } = DefaultFormalDisapprovalStatement;
    public int NextApplicationNumber { get; set; } = FirstApplicationNumber;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public Community Community { get; set; } = null!;

    public static CommunityArcSettings Defaults(Guid communityId) => new() { CommunityId = communityId };
}

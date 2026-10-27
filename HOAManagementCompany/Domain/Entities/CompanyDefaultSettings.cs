using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// CompanyDefaultSettings: the settings a new community starts with (030 FR-025). Copied into the
// community's CommunityArcSettings at creation; later changes never reach existing communities or
// the rules already copied onto their applications (FR-026). Initial values are spec 027's.
// <!-- REPOWISE:END -->

public class CompanyDefaultSettings
{
    public Guid CompanyId { get; set; }
    public int ReviewPeriodDays { get; set; } = CommunityArcSettings.DefaultReviewPeriodDays;
    public ArcLapseRule LapseRule { get; set; } = ArcLapseRule.FlagOverdueOnly;
    public ArcDecisionRule DecisionRule { get; set; } = ArcDecisionRule.MajorityOfMembers;
    public int ReminderDays { get; set; } = CommunityArcSettings.DefaultReminderDays;
    public string TimeZoneId { get; set; } = CommunityArcSettings.DefaultTimeZoneId;
    public string FormalDisapprovalStatement { get; set; } = CommunityArcSettings.DefaultFormalDisapprovalStatement;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public ManagementCompany Company { get; set; } = null!;

    /// <summary>A community's ARC settings, copied from these defaults (FR-009).</summary>
    public CommunityArcSettings CopyTo(Guid communityId) => new()
    {
        CommunityId = communityId,
        ReviewPeriodDays = ReviewPeriodDays,
        LapseRule = LapseRule,
        DecisionRule = DecisionRule,
        ReminderDays = ReminderDays,
        TimeZoneId = TimeZoneId,
        FormalDisapprovalStatement = FormalDisapprovalStatement,
    };

    /// <summary>True when any of the six rule fields differs from <paramref name="settings"/>.</summary>
    public bool DiffersFrom(CommunityArcSettings settings) =>
        ReviewPeriodDays != settings.ReviewPeriodDays
        || LapseRule != settings.LapseRule
        || DecisionRule != settings.DecisionRule
        || ReminderDays != settings.ReminderDays
        || TimeZoneId != settings.TimeZoneId
        || FormalDisapprovalStatement != settings.FormalDisapprovalStatement;
}

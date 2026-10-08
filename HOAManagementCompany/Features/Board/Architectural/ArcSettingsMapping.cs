using HOAManagementCompany.Domain.Entities;

namespace HOAManagementCompany.Features.Board.Architectural;

internal static class ArcSettingsMapping
{
    public static ArcSettingsDto ToDto(CommunityArcSettings s, bool persisted) => new(
        s.ReviewPeriodDays, s.LapseRule.ToString(), s.DecisionRule.ToString(), s.ReminderDays,
        s.TimeZoneId, s.FormalDisapprovalStatement, persisted ? s.UpdatedAt : null);
}

using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>The six architectural review rules, parsed.</summary>
public sealed record ArcRules(
    int ReviewPeriodDays, ArcLapseRule LapseRule, ArcDecisionRule DecisionRule, int ReminderDays,
    string TimeZoneId, string FormalDisapprovalStatement);

/// <summary>A rule that failed, with the request field it belongs to.</summary>
public sealed record ArcRuleError(string Field, string Message);

/// <summary>
/// Validation of the six ARC rule fields, shared by a community's ARC settings (027 FR-029) and the
/// company defaults new communities copy (030 R13): review period 1–365, reminder days 0–30, known
/// enum names, a resolvable IANA zone, and a statement of 1 to 1,000 characters.
/// </summary>
public static class ArcRuleValidator
{
    public const int MaxStatementLength = 1000;

    public static ArcRuleError? Validate(int reviewPeriodDays, string? lapseRule, string? decisionRule, int reminderDays,
        string? timeZoneId, string? formalDisapprovalStatement, out ArcRules rules)
    {
        rules = null!;
        if (reviewPeriodDays is < 1 or > 365)
            return new("reviewPeriodDays", "reviewPeriodDays must be between 1 and 365.");
        if (reminderDays is < 0 or > 30)
            return new("reminderDays", "reminderDays must be between 0 and 30 (0 turns reminders off).");
        if (!ParseEnum(lapseRule, out ArcLapseRule lapse))
            return new("lapseRule", "lapseRule must be FlagOverdueOnly, DeemedApproved or DeemedDenied.");
        if (!ParseEnum(decisionRule, out ArcDecisionRule rule))
            return new("decisionRule", "decisionRule must be MajorityOfMembers or MajorityOfVotesCastWithQuorum.");
        if (string.IsNullOrWhiteSpace(timeZoneId) || !IsTimeZone(timeZoneId.Trim()))
            return new("timeZoneId", "timeZoneId must be a valid IANA time zone, e.g. America/New_York.");
        var statement = formalDisapprovalStatement?.Trim();
        if (string.IsNullOrEmpty(statement))
            return new("formalDisapprovalStatement", "formalDisapprovalStatement is required.");
        if (statement.Length > MaxStatementLength)
            return new("formalDisapprovalStatement",
                $"formalDisapprovalStatement must be at most {MaxStatementLength} characters.");

        rules = new ArcRules(reviewPeriodDays, lapse, rule, reminderDays, timeZoneId.Trim(), statement);
        return null;
    }

    private static bool ParseEnum<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _)
               && Enum.TryParse(value.Trim(), ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    private static bool IsTimeZone(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }
}

using HOAManagementCompany.Features.Board.Architectural;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Architectural;

/// <summary>
/// 031 FR-003 — when a vote-reached decision becomes final: 24 hours after the threshold is met, capped at the end of
/// the due date (or of the decision's own day once the due date has passed), in the application's time zone. Fixed
/// dates, including both 2026 US daylight-saving changes, so the result never depends on the day CI runs.
/// </summary>
public class ArcVotingGraceEndTests
{
    private const string Zone = "America/New_York";

    private static DateTimeOffset Local(int year, int month, int day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById(Zone));

    [Fact]
    public void WellBeforeDueDate_GetsTwentyFourHours()
    {
        var decided = Local(2026, 6, 15, 10);
        Assert.Equal(decided.AddHours(24), ArcVoting.GraceEnd(decided, new DateOnly(2026, 6, 20), Zone));
    }

    [Fact]
    public void EveningBeforeDueDate_GetsTwentyFourHours_WhichEndsBeforeTheDueDateDoes()
    {
        var decided = Local(2026, 6, 19, 20);
        Assert.Equal(decided.AddHours(24), ArcVoting.GraceEnd(decided, new DateOnly(2026, 6, 20), Zone));
    }

    // Due on the day clocks fall back (Sunday 2026-11-01): the grace period is 24 real hours, which ends at 19:00 EST,
    // not at 20:00 on the wall clock.
    [Fact]
    public void EveningBeforeFallBackDueDate_GetsTwentyFourRealHours()
    {
        var decided = Local(2026, 10, 31, 20);
        var end = ArcVoting.GraceEnd(decided, new DateOnly(2026, 11, 1), Zone);
        Assert.Equal(decided.AddHours(24), end);
        Assert.Equal(Local(2026, 11, 1, 19), end);
    }

    [Fact]
    public void OnTheDueDate_LocksAtEndOfThatDay()
    {
        var decided = Local(2026, 6, 20, 15);
        Assert.Equal(Local(2026, 6, 21, 0), ArcVoting.GraceEnd(decided, new DateOnly(2026, 6, 20), Zone));
    }

    // Due on the day clocks spring forward (Sunday 2026-03-08, a 23-hour day): still the end of that day.
    [Fact]
    public void OnSpringForwardDueDate_LocksAtEndOfThatDay()
    {
        var decided = Local(2026, 3, 8, 10);
        Assert.Equal(Local(2026, 3, 9, 0), ArcVoting.GraceEnd(decided, new DateOnly(2026, 3, 8), Zone));
    }

    [Fact]
    public void AfterTheDueDate_LocksAtEndOfTheDecisionDay()
    {
        var decided = Local(2026, 6, 22, 9);
        Assert.Equal(Local(2026, 6, 23, 0), ArcVoting.GraceEnd(decided, new DateOnly(2026, 6, 20), Zone));
    }
}

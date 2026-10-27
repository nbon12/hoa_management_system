using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

// <!-- REPOWISE:START domain=arc-decision -->
// ArcVoting (031): votes can be cast or undone until the board's decision is final. A decision
// reached by votes becomes final one day after the threshold is met, but never later than the end
// of the due date in the application's time zone (a decision made on or after the last day gets
// until the end of that day). Every vote change re-runs the decision rule: losing the threshold
// reopens the application, a new outcome restarts the grace period.
// <!-- REPOWISE:END -->

public static class ArcVoting
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(1);

    /// <summary>When a decision reached at <paramref name="decidedAt"/> stops accepting vote changes.</summary>
    public static DateTimeOffset GraceEnd(DateTimeOffset decidedAt, DateOnly dueDate, string timeZoneId)
    {
        var tz = ArcQueries.ResolveTimeZone(timeZoneId);
        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(decidedAt, tz).DateTime);
        var lastDay = localDay > dueDate ? localDay : dueDate;
        var endOfLastDay = StartOfDay(lastDay.AddDays(1), tz);
        var oneDay = decidedAt + GracePeriod;
        return oneDay < endOfLastDay ? oneDay : endOfLastDay;
    }

    private static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo tz)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A DST jump at midnight skips local 00:00; the day then starts an hour later.
        while (tz.IsInvalidTime(local))
            local = local.AddMinutes(30);
        return new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>The caller's vote can change while the application is undecided or its decision is not yet final.</summary>
    public static bool VotesOpen(ArchitecturalApplication app, DateTimeOffset now) =>
        app.Status == ArcApplicationStatus.Open
        || (app.Status == ArcApplicationStatus.DecisionReached && !app.IsDecisionFinal(now));

    /// <summary>
    /// Re-runs the decision rule after a vote was cast or undone (inside the application's row lock).
    /// Saves nothing; the caller saves.
    /// </summary>
    public static async Task ApplyVotesAsync(
        ApplicationDbContext db, ArcQueries queries, ArchitecturalApplication app, CancellationToken ct)
    {
        var choices = await db.ArchitecturalVotes.Where(v => v.ApplicationId == app.Id).Select(v => v.Choice).ToListAsync(ct);
        var eligible = await queries.EligibleCountAsync(app.CommunityId, app.PropertyId, ct);
        var decision = ArcDecisionRules.Evaluate(
            app.DecisionRule, eligible,
            choices.Count(c => c == ArcVoteChoice.Approve),
            choices.Count(c => c == ArcVoteChoice.RevisionsNeeded),
            choices.Count(c => c == ArcVoteChoice.Deny),
            dueDatePassed: false);
        var now = queries.Now;

        if (decision is null)
        {
            if (app.Status == ArcApplicationStatus.DecisionReached)
                Reopen(app);
            return;
        }

        if (app.Status == ArcApplicationStatus.Open || app.DecisionOutcome != decision.Outcome)
        {
            ArcSweepService.SetDecision(app, decision, ArcDecisionSource.Votes, now, eligible);
            app.DecisionFinalAt = GraceEnd(now, app.DueDate, app.TimeZoneId);
        }
        else
        {
            // Same side still wins: keep the original grace period, refresh wording and board size.
            app.DecisionWording = decision.Wording;
            app.EligibleVoterCount = eligible;
        }
    }

    private static void Reopen(ArchitecturalApplication app)
    {
        app.Status = ArcApplicationStatus.Open;
        app.DecisionOutcome = null;
        app.DecisionWording = null;
        app.DecisionSource = null;
        app.DecisionReachedAt = null;
        app.DecisionFinalAt = null;
        app.EligibleVoterCount = null;
    }
}

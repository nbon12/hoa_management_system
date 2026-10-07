using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

// <!-- REPOWISE:START domain=arc-sweep -->
// ArcSweepService: the hourly, idempotent sweep behind POST /architectural/jobs/sweep
// (027 research R6). (1) Pre-deadline reminders to the board, stamped ReminderSentAt.
// (2) After the end of the due date in the application's snapshotted time zone: under the
// votes-cast rule, decide from votes if quorum is met and a side leads; otherwise apply the
// snapshotted lapse rule once (LapseProcessedAt) and email the board. (3) Drain the outbox.
// Board emails go through IArcNotificationPreferences; outbox DedupKeys prevent duplicates.
// <!-- REPOWISE:END -->

public sealed class ArcSweepService(
    ApplicationDbContext db,
    ArcQueries queries,
    ArcEmailRenderer renderer,
    IArcNotificationPreferences preferences,
    OutboxDispatcher dispatcher,
    ILogger<ArcSweepService> logger)
{
    public async Task<ArcSweepResultDto> RunAsync(CancellationToken ct)
    {
        var now = queries.Now;
        var reminders = 0;
        var lapses = 0;
        var decisions = 0;

        var openIds = await db.ArchitecturalApplications
            .Where(a => a.Status == ArcApplicationStatus.Open
                        && (a.ReminderSentAt == null || a.LapseProcessedAt == null))
            .Select(a => a.Id)
            .ToListAsync(ct);

        foreach (var id in openIds)
        {
            var app = await db.ArchitecturalApplications.Include(a => a.Property).FirstAsync(a => a.Id == id, ct);
            var passed = ArcQueries.IsDueDatePassed(app.DueDate, app.TimeZoneId, now);

            if (passed && app.LapseProcessedAt is null)
            {
                var result = await ProcessDueDateAsync(app, now, ct);
                if (result == DueDateResult.DecidedFromVotes) decisions++;
                if (result == DueDateResult.LapseApplied) lapses++;
            }
            else if (!passed && app.ReminderSentAt is null && await ReminderDueAsync(app, now, ct))
            {
                reminders += await SendRemindersAsync(app, now, ct);
            }
        }

        var dispatched = await dispatcher.DispatchPendingAsync(ct);
        return new ArcSweepResultDto(reminders, lapses, decisions, dispatched);
    }

    private enum DueDateResult { None, DecidedFromVotes, LapseApplied }

    private async Task<bool> ReminderDueAsync(ArchitecturalApplication app, DateTimeOffset now, CancellationToken ct)
    {
        var reminderDays = await db.CommunityArcSettings
            .Where(s => s.CommunityId == app.CommunityId)
            .Select(s => (int?)s.ReminderDays)
            .FirstOrDefaultAsync(ct) ?? CommunityArcSettings.DefaultReminderDays;
        if (reminderDays <= 0)
            return false;

        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, ArcQueries.ResolveTimeZone(app.TimeZoneId)).DateTime);
        return localToday >= app.DueDate.AddDays(-reminderDays);
    }

    private async Task<int> SendRemindersAsync(ArchitecturalApplication app, DateTimeOffset now, CancellationToken ct)
    {
        var queued = 0;
        await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            queued = 0;
            if (app.Status != ArcApplicationStatus.Open || app.ReminderSentAt is not null)
                return null;

            var tally = (await queries.BuildItemAsync(app, string.Empty, ct)).Tally;
            foreach (var (userId, email) in await BoardRecipientsAsync(app, ArcEmailKinds.BoardReminder, ct))
            {
                db.OutboxMessages.Add(ArcEmailRenderer.ToOutbox(
                    ArcEmailKinds.BoardReminder,
                    renderer.BoardReminder(app, app.Property.Address, tally, email),
                    $"arc:{app.Id}:reminder:{userId}", ownerId: null, recipientUserId: userId));
                queued++;
            }

            app.ReminderSentAt = now;
            await db.SaveChangesAsync(ct);
            return null;
        }, ct);
        return queued;
    }

    private async Task<DueDateResult> ProcessDueDateAsync(ArchitecturalApplication app, DateTimeOffset now, CancellationToken ct)
    {
        var result = DueDateResult.None;
        await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            result = DueDateResult.None;
            if (app.Status != ArcApplicationStatus.Open || app.LapseProcessedAt is not null)
                return null;

            app.LapseProcessedAt = now;

            // Votes-cast rule: a leader with quorum at the due date decides; the lapse rule doesn't apply.
            if (app.DecisionRule == ArcDecisionRule.MajorityOfVotesCastWithQuorum)
            {
                var counts = await VoteCountsAsync(app.Id, ct);
                var eligible = await queries.EligibleCountAsync(app.CommunityId, app.PropertyId, ct);
                var decision = ArcDecisionRules.Evaluate(
                    app.DecisionRule, eligible, counts.approve, counts.revisionsNeeded, counts.deny, dueDatePassed: true);
                if (decision is not null)
                {
                    SetDecision(app, decision, ArcDecisionSource.Votes, now);
                    await db.SaveChangesAsync(ct);
                    result = DueDateResult.DecidedFromVotes;
                    return null;
                }
            }

            switch (app.LapseRule)
            {
                case ArcLapseRule.DeemedApproved:
                    SetDecision(app, new ArcDecision(ArcOutcome.Approved, null), ArcDecisionSource.Lapse, now);
                    break;
                case ArcLapseRule.DeemedDenied:
                    // The manager picks the owner wording when recording (FR-025); this default shows until then.
                    SetDecision(app, new ArcDecision(ArcOutcome.Denied, ArcDenialWording.RevisionsRequested),
                        ArcDecisionSource.Lapse, now);
                    break;
            }

            foreach (var (userId, email) in await BoardRecipientsAsync(app, ArcEmailKinds.BoardLapsed, ct))
                db.OutboxMessages.Add(ArcEmailRenderer.ToOutbox(
                    ArcEmailKinds.BoardLapsed,
                    renderer.BoardLapsed(app, app.Property.Address, app.LapseRule, email),
                    $"arc:{app.Id}:lapse:{userId}", ownerId: null, recipientUserId: userId));

            await db.SaveChangesAsync(ct);
            result = DueDateResult.LapseApplied;
            return null;
        }, ct);

        if (result == DueDateResult.LapseApplied)
            ArcLog.LapseApplied(logger, app.CommunityId, app.Id, app.LapseRule.ToString(), now);
        return result;
    }

    public static void SetDecision(ArchitecturalApplication app, ArcDecision decision, ArcDecisionSource source, DateTimeOffset now)
    {
        app.Status = ArcApplicationStatus.DecisionReached;
        app.DecisionOutcome = decision.Outcome;
        app.DecisionWording = decision.Wording;
        app.DecisionSource = source;
        app.DecisionReachedAt = now;
    }

    private async Task<(int approve, int revisionsNeeded, int deny)> VoteCountsAsync(Guid applicationId, CancellationToken ct)
    {
        var choices = await db.ArchitecturalVotes
            .Where(v => v.ApplicationId == applicationId)
            .Select(v => v.Choice)
            .ToListAsync(ct);
        return (choices.Count(c => c == ArcVoteChoice.Approve),
                choices.Count(c => c == ArcVoteChoice.RevisionsNeeded),
                choices.Count(c => c == ArcVoteChoice.Deny));
    }

    private async Task<List<(string userId, string email)>> BoardRecipientsAsync(
        ArchitecturalApplication app, string kind, CancellationToken ct)
    {
        var board = await queries.ActiveBoard(app.CommunityId)
            .Select(m => new { m.UserId, m.User.Email })
            .Distinct()
            .ToListAsync(ct);

        var recipients = new List<(string, string)>();
        foreach (var m in board.Where(m => !string.IsNullOrWhiteSpace(m.Email)))
            if (await preferences.WantsBoardEmailAsync(m.UserId, app.CommunityId, kind, ct))
                recipients.Add((m.UserId, m.Email!));
        return recipients;
    }
}

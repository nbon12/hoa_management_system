using System.Net;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// T056 (US6) — the reminder / lapse sweep driven by <see cref="TestClock"/>. The sweep sees every open
/// application in the shared database, so assertions are per application, never on aggregate counts.
/// </summary>
public class ArcSweepTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private static readonly DateOnly Due = new(2026, 6, 27);

    // 2026-06-27 23:30 in New York (EDT, UTC−4) is 2026-06-28 03:30 UTC.
    private static readonly DateTimeOffset LateOnDueDate = new(2026, 6, 28, 3, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterDueDate = new(2026, 6, 28, 4, 30, 0, TimeSpan.Zero);

    private async Task SweepAsync()
    {
        using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<ArcSweepService>().RunAsync(default);
    }

    private Task<List<Domain.Entities.OutboxMessage>> RowsAsync(Guid appId, string kind) =>
        WithDbAsync(db => db.OutboxMessages.AsNoTracking()
            .Where(m => m.Kind == kind && m.DedupKey!.StartsWith($"arc:{appId}:")).ToListAsync());

    private Task<Domain.Entities.ArchitecturalApplication> AppAsync(Guid id) =>
        WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().SingleAsync(a => a.Id == id));

    private async Task<Guid> ArrangeAsync(ArcScenario s, ArcLapseRule lapse = ArcLapseRule.FlagOverdueOnly,
        ArcDecisionRule rule = ArcDecisionRule.MajorityOfMembers)
    {
        Clock.UtcNow = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        return await CreateApplicationAsync(s, new AppSpec
        {
            Received = new DateOnly(2026, 5, 28), Due = Due, LapseRule = lapse, DecisionRule = rule
        });
    }

    // US6-S13: "Given a community with a reminder of 7 days and an open application due 06/27/26 with no decision, When
    // 06/20/26 arrives, Then every active board member is emailed one reminder with the application ID, project, due
    // date, current tally and a link. An application that already has a decision gets no reminder."
    [Fact]
    public async Task Reminder_SentOncePerBoardMember_SevenDaysBeforeDueDate()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await ArrangeAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));
        var decided = await CreateApplicationAsync(s, new AppSpec
        {
            Due = Due, Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved
        });
        var app = await AppAsync(appId);

        Clock.UtcNow = new DateTimeOffset(2026, 6, 19, 16, 0, 0, TimeSpan.Zero);
        await SweepAsync();
        Assert.Empty(await RowsAsync(appId, ArcEmailKinds.BoardReminder));

        Clock.UtcNow = new DateTimeOffset(2026, 6, 20, 16, 0, 0, TimeSpan.Zero);
        await SweepAsync();
        await SweepAsync();

        var rows = await RowsAsync(appId, ArcEmailKinds.BoardReminder);
        Assert.Equal(s.Board.Select(b => b.UserId).OrderBy(x => x), rows.Select(r => r.RecipientUserId!).OrderBy(x => x));
        Assert.All(rows, r => Assert.Null(r.OwnerId));
        Assert.All(rows, r =>
        {
            var msg = System.Text.Json.JsonSerializer.Deserialize<Infrastructure.Payments.Alerts.AlertMessage>(r.PayloadJson)!;
            Assert.Equal($"Decision due 06/27/26: {app.DisplayId}", msg.Subject);
            Assert.Contains($"A decision on {app.DisplayId} is due 06/27/26.", msg.Body);
            Assert.Contains(app.ProjectTitle, msg.Body);
            Assert.Contains("Votes so far: 1 approve, 0 revisions needed, 0 deny, 2 not voted.", msg.Body);
            Assert.Contains($"/app/board/architectural?open={appId}", msg.Body);
        });
        Assert.Empty(await RowsAsync(decided, ArcEmailKinds.BoardReminder));
    }

    [Fact]
    public async Task Reminder_IsOff_WhenReminderDaysIsZero()
    {
        var s = await CreateScenarioAsync(2);
        var appId = await ArrangeAsync(s);
        await WithDbAsync(async db =>
        {
            var settings = Domain.Entities.CommunityArcSettings.Defaults(s.CommunityId);
            settings.ReminderDays = 0;
            db.CommunityArcSettings.Add(settings);
            return await db.SaveChangesAsync();
        });

        Clock.UtcNow = new DateTimeOffset(2026, 6, 26, 16, 0, 0, TimeSpan.Zero);
        await SweepAsync();

        Assert.Empty(await RowsAsync(appId, ArcEmailKinds.BoardReminder));
    }

    // US6-S14: "Given a community whose lapse rule is "flag overdue only", When an open application passes its due date
    // with no decision, Then it is shown as overdue, voting stays open, nothing is decided automatically, and every
    // active board member of the community is emailed that the application is overdue."
    [Fact]
    public async Task FlagOverdueOnly_StaysOpenAndOverdue_VotingContinues_BoardEmailedOnce()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await ArrangeAsync(s);

        Clock.UtcNow = AfterDueDate;
        await SweepAsync();
        await SweepAsync();

        var app = await AppAsync(appId);
        Assert.Equal(ArcApplicationStatus.Open, app.Status);
        Assert.Null(app.DecisionOutcome);
        Assert.Equal(3, (await RowsAsync(appId, ArcEmailKinds.BoardLapsed)).Count);
        Assert.Contains("overdue", (await RowsAsync(appId, ArcEmailKinds.BoardLapsed))[0].PayloadJson);

        await LoginAsAsync(s.Board[0]);
        var row = (await ListAsync(s.CommunityId)).Items.Single(i => i.Id == appId);
        Assert.True(row.Overdue);
        Assert.Equal(HttpStatusCode.Created, (await VoteAsync(s.CommunityId, appId, "Approve")).StatusCode);
    }

    // US6-S15: "…lapse rule is "deemed approved"… Then the application shows "decision reached: approved by default
    // (review period lapsed)", refuses further votes, and every active board member is emailed…"
    // US6-S16: "…lapse rule is "deemed denied"… "decision reached: denied by default (review period lapsed)"…"
    [Theory]
    [InlineData(ArcLapseRule.DeemedApproved, ArcOutcome.Approved, "approved by default")]
    [InlineData(ArcLapseRule.DeemedDenied, ArcOutcome.Denied, "denied by default")]
    public async Task DeemedRules_DecideByLapse_RefuseVotes_AndEmailBoard(ArcLapseRule rule, ArcOutcome outcome, string label)
    {
        var s = await CreateScenarioAsync(3);
        var appId = await ArrangeAsync(s, rule);

        Clock.UtcNow = AfterDueDate;
        await SweepAsync();

        var app = await AppAsync(appId);
        Assert.Equal(ArcApplicationStatus.DecisionReached, app.Status);
        Assert.Equal(outcome, app.DecisionOutcome);
        Assert.Equal(ArcDecisionSource.Lapse, app.DecisionSource);
        var emails = await RowsAsync(appId, ArcEmailKinds.BoardLapsed);
        Assert.Equal(3, emails.Count);
        Assert.Contains(label, emails[0].PayloadJson);

        await LoginAsAsync(s.Board[0]);
        var res = await VoteAsync(s.CommunityId, appId, "Approve");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(ArcErrorCodes.ApplicationDecided, await ErrorCodeAsync(res));
    }

    // US6-S5: "…majority of votes cast with 5 eligible members and votes of 2 approve / 1 deny, When the due date passes
    // with no more votes, Then the decision "approve" is reached and the lapse rule does not apply."
    [Fact]
    public async Task VotesCast_LeaderWithQuorumAtDueDate_DecidesFromVotes_NoLapse()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await ArrangeAsync(s, ArcLapseRule.DeemedDenied, ArcDecisionRule.MajorityOfVotesCastWithQuorum);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve), (s.Board[2], ArcVoteChoice.Deny));

        Clock.UtcNow = AfterDueDate;
        await SweepAsync();

        var app = await AppAsync(appId);
        Assert.Equal(ArcOutcome.Approved, app.DecisionOutcome);
        Assert.Equal(ArcDecisionSource.Votes, app.DecisionSource);
        Assert.Empty(await RowsAsync(appId, ArcEmailKinds.BoardLapsed));
    }

    // US6-S6: "…majority of votes cast with 5 eligible members and only 2 votes cast, When the due date passes, Then
    // quorum isn't met, no decision is reached from votes, and the community's lapse rule applies."
    [Fact]
    public async Task VotesCast_NoQuorumAtDueDate_AppliesLapseRule()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await ArrangeAsync(s, ArcLapseRule.DeemedDenied, ArcDecisionRule.MajorityOfVotesCastWithQuorum);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));

        Clock.UtcNow = AfterDueDate;
        await SweepAsync();

        var app = await AppAsync(appId);
        Assert.Equal(ArcOutcome.Denied, app.DecisionOutcome);
        Assert.Equal(ArcDecisionSource.Lapse, app.DecisionSource);
        Assert.Equal(5, (await RowsAsync(appId, ArcEmailKinds.BoardLapsed)).Count);
    }

    [Fact]
    public async Task DueDate_IsJudgedInTheApplicationsTimeZone()
    {
        var s = await CreateScenarioAsync(1);
        var appId = await ArrangeAsync(s, ArcLapseRule.DeemedApproved);

        Clock.UtcNow = LateOnDueDate;
        await SweepAsync();
        Assert.Equal(ArcApplicationStatus.Open, (await AppAsync(appId)).Status);

        Clock.UtcNow = AfterDueDate;
        await SweepAsync();
        Assert.Equal(ArcApplicationStatus.DecisionReached, (await AppAsync(appId)).Status);
    }
}

/// <summary>Opt-out seam for the Notification Settings spec: an excluded user gets no board email.</summary>
public class ArcSweepPreferencesTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private static readonly HashSet<string> OptedOut = [];

    private sealed class OptOut : IArcNotificationPreferences
    {
        public Task<bool> WantsBoardEmailAsync(string userId, Guid communityId, string kind, CancellationToken ct) =>
            Task.FromResult(!OptedOut.Contains(userId));
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.RemoveAll<IArcNotificationPreferences>();
        services.AddSingleton<IArcNotificationPreferences, OptOut>();
    }

    [Fact]
    public async Task OptedOutBoardMember_GetsNoLapseEmail_OthersDo()
    {
        var s = await CreateScenarioAsync(3);
        OptedOut.Add(s.Board[0].UserId);
        Clock.UtcNow = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var appId = await CreateApplicationAsync(s, new AppSpec { Received = new DateOnly(2026, 5, 1), Due = new DateOnly(2026, 5, 31) });

        Clock.UtcNow = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        using (var scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<ArcSweepService>().RunAsync(default);

        var recipients = await WithDbAsync(db => db.OutboxMessages
            .Where(m => m.Kind == ArcEmailKinds.BoardLapsed && m.DedupKey!.StartsWith($"arc:{appId}:"))
            .Select(m => m.RecipientUserId).ToListAsync());
        Assert.Equal(2, recipients.Count);
        Assert.DoesNotContain(s.Board[0].UserId, recipients);
    }
}

using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// 031 US3 — undo and recast a vote until the board's decision is final: one day after the threshold is
/// met, but never past the end of the due date (specs/031-arc-review-refinements/spec.md).
/// </summary>
public class VoteChangeTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private const string Zone = "America/New_York";

    private Task<HttpResponseMessage> UndoAsync(Guid communityId, Guid applicationId) =>
        Client.DeleteAsync($"{AppUrl(communityId, applicationId)}/votes/mine");

    private async Task<ArcListItemDto> CastAsync(BoardMember who, ArcScenario s, Guid appId, string choice)
    {
        await LoginAsAsync(who);
        var res = await VoteAsync(s.CommunityId, appId, choice);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
    }

    /// <summary>Local wall-clock time in the test zone as a UTC instant.</summary>
    private static DateTimeOffset Local(DateOnly day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, 0)), TimeZoneInfo.FindSystemTimeZoneById(Zone));

    // 031 US3-S1: "Given I voted on an open application that has no decision, When I click undo, Then my vote is removed,
    // the tally drops by one and I can vote again."
    [Fact]
    public async Task Undo_BeforeDecision_RemovesVote_AndAllowsRecast()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        var voted = await CastAsync(s.Board[0], s, appId, "Approve");
        Assert.True(voted.CanUndoVote);

        var res = await UndoAsync(s.CommunityId, appId);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var row = (await res.Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
        Assert.Equal(new ArcMyVoteDto(ArcVoteStates.CanVote), row.MyVote);
        Assert.Equal(new ArcTallyDto(0, 0, 0, 5, 5), row.Tally);
        Assert.False(row.CanUndoVote);
        Assert.False(await WithDbAsync(db => db.ArchitecturalVotes.AnyAsync(v => v.ApplicationId == appId)));

        var recast = await CastAsync(s.Board[0], s, appId, "Deny");
        Assert.Equal(new ArcMyVoteDto(ArcVoteStates.Voted, "Deny"), recast.MyVote);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcVoteUndone"));
    }

    // 031 US3-S2: "Given my vote reached the threshold less than a day ago (and before the due date's last day), When I
    // undo it, Then the decision is withdrawn and the application is open again."
    [Fact]
    public async Task Undo_DuringGrace_BelowThreshold_ReopensApplication()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));
        var decided = await CastAsync(s.Board[2], s, appId, "Approve");
        Assert.Equal("DecisionReached", decided.Status);
        Assert.Equal(Clock.UtcNow + ArcVoting.GracePeriod, decided.VotesLockAt);
        Assert.True(decided.CanUndoVote);

        Clock.UtcNow = Clock.UtcNow.AddHours(23);
        var res = await UndoAsync(s.CommunityId, appId);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var row = (await res.Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
        Assert.Equal("Open", row.Status);
        Assert.Null(row.Decision);
        Assert.Null(row.VotesLockAt);
        var app = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().SingleAsync(a => a.Id == appId));
        Assert.Null(app.DecisionOutcome);
        Assert.Null(app.DecisionFinalAt);
    }

    // 031 US3-S3: "Given the decision would still hold without my vote, When I undo it during the grace period, Then the
    // decision stands with its original deadline, and I can recast my vote until then."
    [Fact]
    public async Task Undo_DuringGrace_AboveThreshold_KeepsDecision_AndAllowsRecast()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));
        var decided = await CastAsync(s.Board[2], s, appId, "Approve");
        await CastAsync(s.Board[3], s, appId, "Approve");

        Clock.UtcNow = Clock.UtcNow.AddHours(2);
        var undone = (await (await UndoAsync(s.CommunityId, appId)).Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
        Assert.Equal("DecisionReached", undone.Status);
        Assert.Equal(decided.VotesLockAt!.Value, undone.VotesLockAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Equal(ArcVoteStates.CanVote, undone.MyVote.State);

        var recast = await CastAsync(s.Board[3], s, appId, "Deny");
        Assert.Equal(new ArcTallyDto(3, 0, 1, 1, 5), recast.Tally);
        Assert.Equal(new ArcDecisionDto("Approved", null, "Votes"), recast.Decision);
    }

    // 031 US3-S4: "Given the decision's grace period has ended, When I try to undo or cast a vote, Then the request is
    // refused with APPLICATION_DECIDED and no undo link is offered."
    [Fact]
    public async Task AfterGrace_UndoAndVote_AreRefused()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));
        var decided = await CastAsync(s.Board[2], s, appId, "Approve");

        Clock.UtcNow = decided.VotesLockAt!.Value;
        var undo = await UndoAsync(s.CommunityId, appId);
        Assert.Equal(HttpStatusCode.Conflict, undo.StatusCode);
        Assert.Equal(ArcErrorCodes.ApplicationDecided, await ErrorCodeAsync(undo));

        var row = (await ListAsync(s.CommunityId)).Items.Single();
        Assert.False(row.CanUndoVote);
        Assert.True(row.DecisionFinal);
        Assert.Null(row.VotesLockAt);
    }

    // 031 FR-003: "the last day to vote gets no extra day — only until the end of that day."
    [Fact]
    public async Task DecisionOnLastDay_LocksAtEndOfDueDate()
    {
        var due = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(3);
        Clock.UtcNow = Local(due, 15);
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Due = due, Received = due.AddDays(-30), TimeZoneId = Zone });
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Deny));

        var decided = await CastAsync(s.Board[1], s, appId, "Deny");

        Assert.Equal(Local(due.AddDays(1), 0), decided.VotesLockAt);
    }

    // 031 FR-003: a decision made the evening before the due date still gets its full day (24 hours), which ends before
    // the due date does. The day is 24 real hours, not the same wall-clock time tomorrow: they differ when a
    // daylight-saving change falls in between (ArcVotingGraceEndTests pins that case to fixed dates).
    [Fact]
    public async Task DecisionOnDayBeforeDue_GetsFullDay()
    {
        var due = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(3);
        Clock.UtcNow = Local(due.AddDays(-1), 20);
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Due = due, Received = due.AddDays(-30), TimeZoneId = Zone });
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));

        var decided = await CastAsync(s.Board[1], s, appId, "Approve");

        Assert.Equal(Clock.UtcNow + ArcVoting.GracePeriod, decided.VotesLockAt);
        Assert.True(decided.VotesLockAt < Local(due.AddDays(1), 0));
    }

    // 031 FR-003: an overdue application (flag-only lapse rule) decided after its due date is changeable to the end of that day.
    [Fact]
    public async Task DecisionAfterDueDate_LocksAtEndOfThatDay()
    {
        var due = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(-2);
        var today = due.AddDays(2);
        Clock.UtcNow = Local(today, 9);
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Due = due, Received = due.AddDays(-30), TimeZoneId = Zone });
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));

        var decided = await CastAsync(s.Board[1], s, appId, "Approve");

        Assert.Equal(Local(today.AddDays(1), 0), decided.VotesLockAt);
    }

    // 031 US3-S5: "Given I have not voted, When I click undo, Then the request is refused with NOT_VOTED."
    [Fact]
    public async Task Undo_WithoutVote_IsRefused()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var res = await UndoAsync(s.CommunityId, appId);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(ArcErrorCodes.NotVoted, await ErrorCodeAsync(res));
    }

    // 031 US3-S6: "Given an application is closed, When I try to undo my vote, Then it is refused with APPLICATION_CLOSED."
    [Fact]
    public async Task Undo_OnClosedApplication_IsRefused()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[0]);

        var res = await UndoAsync(s.CommunityId, appId);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(ArcErrorCodes.ApplicationClosed, await ErrorCodeAsync(res));
    }

    // 031 US3-S7: "Given a decision is still within its grace period, When the manager records the outcome, Then it is
    // refused with DECISION_NOT_FINAL."
    [Fact]
    public async Task RecordOutcome_DuringGrace_IsRefused()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));
        await CastAsync(s.Board[1], s, appId, "Approve");
        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);

        var res = await Client.PostAsJsonAsync($"{AppUrl(s.CommunityId, appId)}/outcome", new { });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(ArcErrorCodes.DecisionNotFinal, await ErrorCodeAsync(res));
    }

    // 031 US3-S8: a non-board user can't undo (same capability as voting).
    [Fact]
    public async Task Undo_ByManager_IsRefused()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);

        Assert.Equal(HttpStatusCode.Forbidden, (await UndoAsync(s.CommunityId, appId)).StatusCode);
    }
}

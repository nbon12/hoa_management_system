using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T032 (US2) — POST …/votes.</summary>
public class CastVoteEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private static bool Scalar(LogEvent e, string name, object expected) =>
        e.Properties.TryGetValue(name, out var v) && v is ScalarValue sv && Equals(sv.Value?.ToString(), expected.ToString());

    // US2-S1: "Given an open application I have not voted on, When the row renders, Then my vote column shows
    // Approve, Revisions needed, Deny and Info buttons." (backend: myVote.state == CanVote)
    [Fact]
    public async Task UnvotedBoardMember_CanVote()
    {
        var s = await CreateScenarioAsync(3);
        await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);
        Assert.Equal(ArcVoteStates.CanVote, (await ListAsync(s.CommunityId)).Items.Single().MyVote.State);
    }

    // US2-S2: "Given an open application I have not voted on with a tally of 2 approve / 0 deny / 3 not voted,
    // When I click Approve on its row, Then my vote is saved, my vote column reads "you voted approve", and the
    // tally reads 3 approve / 0 deny / 2 not voted."
    [Fact]
    public async Task Approve_IsSaved_AndTallyUpdates()
    {
        // 6-member board so a third approve does not yet reach a decision (majority is 4).
        var s = await CreateScenarioAsync(6);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[1], ArcVoteChoice.Approve), (s.Board[2], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[0]);

        var res = await VoteAsync(s.CommunityId, appId, "Approve");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var row = (await res.Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
        Assert.Equal(new ArcMyVoteDto(ArcVoteStates.Voted, "Approve"), row.MyVote);
        Assert.Equal(3, row.Tally.Approve);
        Assert.Equal(0, row.Tally.Deny);
        Assert.Equal(3, row.Tally.NotVoted);
        Assert.True(await WithDbAsync(db => db.ArchitecturalVotes.AnyAsync(v =>
            v.ApplicationId == appId && v.VoterUserId == s.Board[0].UserId && v.Choice == ArcVoteChoice.Approve)));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcVoteCast")
            && Scalar(e, "ActorId", s.Board[0].UserId) && Scalar(e, "ApplicationId", appId));
    }

    // US2-S3: "Given I have already voted deny on an application, When the row renders, Then my vote column
    // shows "you voted deny" and no vote buttons." Also: a second vote is refused (FR-016).
    [Fact]
    public async Task AfterDeny_StateIsVotedDeny_AndSecondVoteIsRefused()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);
        (await VoteAsync(s.CommunityId, appId, "Deny")).EnsureSuccessStatusCode();

        Assert.Equal(new ArcMyVoteDto(ArcVoteStates.Voted, "Deny"), (await ListAsync(s.CommunityId)).Items.Single().MyVote);

        var again = await VoteAsync(s.CommunityId, appId, "Approve");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ArcErrorCodes.AlreadyVoted, await ErrorCodeAsync(again));
    }

    // US2-S4: "When I click Revisions needed with the comment "Fence must be 5ft max per Guideline 4.2", Then my
    // vote is saved, my vote column reads "you voted revisions needed", the tally's revisions-needed count goes up by one…"
    [Fact]
    public async Task RevisionsNeeded_WithComment_IsSaved()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var row = (await (await VoteAsync(s.CommunityId, appId, "RevisionsNeeded", "Fence must be 5ft max per Guideline 4.2"))
            .Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;

        Assert.Equal(new ArcMyVoteDto(ArcVoteStates.Voted, "RevisionsNeeded"), row.MyVote);
        Assert.Equal(1, row.Tally.RevisionsNeeded);
        var vote = Assert.Single((await DetailAsync(s.CommunityId, appId)).Votes);
        Assert.Equal("Fence must be 5ft max per Guideline 4.2", vote.Comment);
    }

    // US2-S5: "When I enter the comment "Fence height exceeds the 5ft limit in §4.2" and click Deny, Then my deny
    // vote is saved with that comment, and another board member of the same community who opens the application
    // sees the comment with my name."
    [Fact]
    public async Task DenyComment_IsVisibleToAnotherBoardMember_WithVoterName()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);
        (await VoteAsync(s.CommunityId, appId, "Deny", "Fence height exceeds the 5ft limit in §4.2")).EnsureSuccessStatusCode();

        await LoginAsAsync(s.Board[1]);
        var vote = Assert.Single((await DetailAsync(s.CommunityId, appId)).Votes);
        Assert.Equal("Deny", vote.Choice);
        Assert.Equal("Fence height exceeds the 5ft limit in §4.2", vote.Comment);
        Assert.Equal("Board Tester", vote.VoterName);
    }

    // US2-S6: "Given I click Deny without entering a comment, When the vote is saved, Then it succeeds; the comment is optional."
    [Fact]
    public async Task Deny_WithoutComment_Succeeds()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        Assert.Equal(HttpStatusCode.Created, (await VoteAsync(s.CommunityId, appId, "Deny")).StatusCode);
        Assert.Null(Assert.Single((await DetailAsync(s.CommunityId, appId)).Votes).Comment);
    }

    // US2-S7: "Given an application is closed, When I try to vote on it, Then the vote is refused and the existing tally is unchanged."
    [Fact]
    public async Task VoteOnClosedApplication_IsRefused_TallyUnchanged()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        await AddVotesAsync(appId, (s.Board[1], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[0]);

        var res = await VoteAsync(s.CommunityId, appId, "Deny");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(ArcErrorCodes.ApplicationClosed, await ErrorCodeAsync(res));
        Assert.Equal(1, await WithDbAsync(db => db.ArchitecturalVotes.CountAsync(v => v.ApplicationId == appId)));
    }

    // US2-S8: "Given an application for a property I own, When the row renders, Then I am shown as recused instead
    // of being offered vote buttons, and a vote attempt from me is refused."
    [Fact]
    public async Task Owner_IsRecused_AndExcludedFromEligible()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await LinkUserToPropertyAsync(s.Board[0].UserId, s.PropertyId);
        await LoginAsAsync(s.Board[0]);

        var row = (await ListAsync(s.CommunityId)).Items.Single();
        Assert.Equal(ArcVoteStates.Recused, row.MyVote.State);
        Assert.Equal(4, row.Tally.Eligible);

        var res = await VoteAsync(s.CommunityId, appId, "Approve");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(ArcErrorCodes.Recused, await ErrorCodeAsync(res));
        Assert.Equal(0, await WithDbAsync(db => db.ArchitecturalVotes.CountAsync(v => v.ApplicationId == appId)));
    }

    // US2-S9: "Given I hold a Community Manager or Accountant membership but no Board Member membership, When I try
    // to vote, Then the vote is refused."
    [Theory]
    [InlineData(CommunityRole.CommunityManager)]
    [InlineData(CommunityRole.Accountant)]
    public async Task NonBoardRoles_CannotVote(CommunityRole role)
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        var member = await CreateMemberAsync(s.CommunityId, role);
        await LoginAsAsync(member);

        var res = await VoteAsync(s.CommunityId, appId, "Approve");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", await ErrorCodeAsync(res));
        Assert.Equal(0, await WithDbAsync(db => db.ArchitecturalVotes.CountAsync(v => v.ApplicationId == appId)));
    }

    [Theory]
    [InlineData("Maybe")]
    [InlineData("1")]
    [InlineData("")]
    public async Task InvalidChoice_Returns422(string choice)
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var res = await VoteAsync(s.CommunityId, appId, choice);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task OverlongComment_Returns422()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var res = await VoteAsync(s.CommunityId, appId, "Deny", new string('x', 2001));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task ApplicationFromAnotherCommunity_IsRefused()
    {
        var a = await CreateScenarioAsync(1);
        var b = await CreateScenarioAsync(1);
        var appInB = await CreateApplicationAsync(b);
        await LoginAsAsync(a.Board[0]);

        Assert.Equal(HttpStatusCode.Forbidden, (await VoteAsync(a.CommunityId, appInB, "Approve")).StatusCode);
    }
}

/// <summary>Rate limit on board writes (027 R9) with a tiny budget so the 429 is deterministic.</summary>
public class BoardWritesRateLimitTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        [new("RateLimiting:BoardWritesPermitsPerMinute", "2")];

    [Fact]
    public async Task ExceedingBoardWritesBudget_Returns429()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await VoteAsync(s.CommunityId, appId, "Maybe")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}

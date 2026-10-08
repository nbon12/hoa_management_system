using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T052 (US6) — decisions reached through the vote endpoint on a real database.</summary>
public class DecisionOnVoteTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private async Task<ArcListItemDto> VoteAs(BoardMember who, ArcScenario s, Guid appId, string choice)
    {
        await LoginAsAsync(who);
        var res = await VoteAsync(s.CommunityId, appId, choice);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
    }

    // US6-S1: "Given a community using the default majority-of-members rule with 5 active, non-recused board members
    // and an application with 2 approve votes, When a third board member votes approve, Then the application shows
    // "decision reached: approve" and refuses further votes."
    [Fact]
    public async Task ThirdApproveOfFive_DecidesApproved_AndRefusesFurtherVotes()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));

        var row = await VoteAs(s.Board[2], s, appId, "Approve");

        Assert.Equal("DecisionReached", row.Status);
        Assert.Equal(new ArcDecisionDto("Approved", null, "Votes"), row.Decision);
        await LoginAsAsync(s.Board[3]);
        var late = await VoteAsync(s.CommunityId, appId, "Deny");
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal(ArcErrorCodes.ApplicationDecided, await ErrorCodeAsync(late));
    }

    // US6-S2: "Given the same community, When 3 board members vote deny, Then the application shows "decision reached: denied"."
    [Fact]
    public async Task ThreeDenies_DecideDenied_WithDeniedWording()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Deny), (s.Board[1], ArcVoteChoice.Deny));

        var row = await VoteAs(s.Board[2], s, appId, "Deny");

        Assert.Equal(new ArcDecisionDto("Denied", "Denied", "Votes"), row.Decision);
    }

    // US6-S3: "Given 2 approve and 2 deny votes out of 5, When the row renders, Then no decision is shown and voting stays open."
    [Fact]
    public async Task TwoAndTwoOfFive_HasNoDecision_AndVotingStaysOpen()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Deny));
        await VoteAs(s.Board[2], s, appId, "Approve");

        var row = await VoteAs(s.Board[3], s, appId, "Deny");

        Assert.Null(row.Decision);
        Assert.Equal("Open", row.Status);
        await LoginAsAsync(s.Board[4]);
        Assert.Equal(ArcVoteStates.CanVote, (await ListAsync(s.CommunityId)).Items.Single().MyVote.State);
    }

    // US6-S4: "Given a community using majority of votes cast with 5 eligible members, When 3 members have voted
    // 3 approve / 0 deny, Then the decision "approve" is reached immediately, because the 2 remaining votes can't overtake it."
    [Fact]
    public async Task VotesCast_ThreeApproveOfFive_DecidesImmediately()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s, new AppSpec { DecisionRule = ArcDecisionRule.MajorityOfVotesCastWithQuorum });
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));

        var row = await VoteAs(s.Board[2], s, appId, "Approve");

        Assert.Equal("Approved", row.Decision?.Outcome);
    }

    // US6-S9: "Given a 5-member board (majority of members) with votes of 1 revisions needed, 1 deny and 1 approve,
    // When another member votes revisions needed, Then the denial side holds 3 of 5, revisions needed (2) outnumbers
    // deny (1), and the application shows "decision reached: denied · revisions requested"."
    [Fact]
    public async Task RevisionsNeededAndDeny_CountTogether_WordingFollowsMajorityKind()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId,
            (s.Board[0], ArcVoteChoice.RevisionsNeeded), (s.Board[1], ArcVoteChoice.Deny), (s.Board[2], ArcVoteChoice.Approve));

        var row = await VoteAs(s.Board[3], s, appId, "RevisionsNeeded");

        Assert.Equal(new ArcDecisionDto("Denied", "RevisionsRequested", "Votes"), row.Decision);
    }

    // Edge case "Board size changes mid-vote": a departed member's vote still counts; eligible drops by one; a newly
    // added member can vote on the still-open application.
    [Fact]
    public async Task DepartedMembersVoteStillCounts_AndNewMemberCanVote()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));
        await WithDbAsync(async db =>
        {
            var m = await db.CommunityMemberships.SingleAsync(x => x.UserId == s.Board[0].UserId && x.CommunityId == s.CommunityId);
            m.EndDate = Today.AddDays(-1);
            return await db.SaveChangesAsync();
        });

        await LoginAsAsync(s.Board[1]);
        var tally = (await ListAsync(s.CommunityId)).Items.Single().Tally;
        Assert.Equal(1, tally.Approve);
        Assert.Equal(4, tally.Eligible);

        var newcomer = await CreateMemberAsync(s.CommunityId, CommunityRole.BoardMember);
        await LoginAsAsync(newcomer);
        Assert.Equal(HttpStatusCode.Created, (await VoteAsync(s.CommunityId, appId, "Approve")).StatusCode);
    }
}

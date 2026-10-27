using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// 031 US5/US6 — recusal never empties the board, a final decision keeps the board size it was decided by, and a
/// lapse decision explains itself by citing the governing documents.
/// </summary>
public class BoardSizeAndLapseTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    // 031 US5-S1: "Given a board with a single member who owns the application's property, When the row renders, Then
    // the member is not recused, can vote, and the tally reads 0/1 with the rule text 'One of one votes decide.'"
    [Fact]
    public async Task SingleMemberBoard_OwnerIsNotRecused_AndCanVote()
    {
        var s = await CreateScenarioAsync(1);
        var appId = await CreateApplicationAsync(s);
        await LinkUserToPropertyAsync(s.Board[0].UserId, s.PropertyId);
        await LoginAsAsync(s.Board[0]);

        var list = await ListAsync(s.CommunityId);
        var row = list.Items.Single();
        Assert.Equal(ArcVoteStates.CanVote, row.MyVote.State);
        Assert.Equal(new ArcTallyDto(0, 0, 0, 1, 1), row.Tally);
        Assert.Equal(1, list.Counts.AwaitingMyVote);
        Assert.StartsWith("One of one votes decide.", (await DetailAsync(s.CommunityId, appId)).RuleText);

        var res = await VoteAsync(s.CommunityId, appId, "Approve");
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(new ArcDecisionDto("Approved", null, "Votes"),
            (await res.Content.ReadFromJsonAsync<ArcListItemDto>(Json))!.Decision);
    }

    // 031 US5-S2: "Given a two-member board where one member owns the property, When that member views it, Then they are
    // recused and only the other member counts (0/1)."
    [Fact]
    public async Task TwoMemberBoard_OwnerIsStillRecused()
    {
        var s = await CreateScenarioAsync(2);
        await CreateApplicationAsync(s);
        await LinkUserToPropertyAsync(s.Board[0].UserId, s.PropertyId);
        await LoginAsAsync(s.Board[0]);

        var row = (await ListAsync(s.CommunityId)).Items.Single();

        Assert.Equal(ArcVoteStates.Recused, row.MyVote.State);
        Assert.Equal(1, row.Tally.Eligible);
    }

    // 031 US5-S3: "Given an application was decided by a 3-member board, When two more members join, Then its final
    // tally still shows 3 slots (3/3), while an open application shows all 5."
    [Fact]
    public async Task FinalDecision_KeepsBoardSizeAtDecision()
    {
        var s = await CreateScenarioAsync(3);
        var decidedId = await CreateApplicationAsync(s);
        var openId = await CreateApplicationAsync(s);
        await AddVotesAsync(decidedId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[2]);
        var decided = (await (await VoteAsync(s.CommunityId, decidedId, "Approve")).Content.ReadFromJsonAsync<ArcListItemDto>(Json))!;
        Clock.UtcNow = decided.VotesLockAt!.Value.AddMinutes(1);

        await CreateMemberAsync(s.CommunityId, CommunityRole.BoardMember);
        await CreateMemberAsync(s.CommunityId, CommunityRole.BoardMember);

        var items = (await ListAsync(s.CommunityId)).Items;
        Assert.Equal(new ArcTallyDto(3, 0, 0, 0, 3), items.Single(i => i.Id == decidedId).Tally);
        Assert.Equal(5, items.Single(i => i.Id == openId).Tally.Eligible);
    }

    // 031 US6-S1: "Given an application approved by default because the board did not act by its due date, When a board
    // member opens it, Then the panel shows a statement citing the community's governing-document clause."
    [Fact]
    public async Task LapseApproval_DetailCitesGoverningDocuments()
    {
        var s = await CreateScenarioAsync(3);
        var received = new DateOnly(2026, 8, 1);
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved, Source = ArcDecisionSource.Lapse,
            LapseRule = ArcLapseRule.DeemedApproved, Received = received, Due = received.AddDays(30)
        });
        await WithDbAsync(async db =>
        {
            db.CommunityArcSettings.Add(new CommunityArcSettings
            {
                CommunityId = s.CommunityId, LapseRuleCitation = "Article IX, Section 9.4 of the Declaration"
            });
            return await db.SaveChangesAsync();
        });
        await LoginAsAsync(s.Board[0]);

        var detail = await DetailAsync(s.CommunityId, appId);

        Assert.Equal(
            "Approved by default: the board did not approve or deny this application by its due date (08/31/26). Under " +
            "Article IX, Section 9.4 of the Declaration, an application the board does not act on within the 30-day " +
            "review period is deemed approved.",
            detail.LapseStatement);
        Assert.Equal(new ArcDecisionDto("Approved", null, "Lapse"), detail.Decision);
    }

    // 031 US6-S2: a decision reached by votes has no lapse statement.
    [Fact]
    public async Task VoteDecision_HasNoLapseStatement()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        await LoginAsAsync(s.Board[0]);

        Assert.Null((await DetailAsync(s.CommunityId, appId)).LapseStatement);
    }

    // 031 FR-016: the manager sets the citation with the other ARC settings.
    [Fact]
    public async Task Settings_SaveLapseRuleCitation()
    {
        var s = await CreateScenarioAsync(1);
        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);

        var res = await Client.PutAsJsonAsync($"/api/v1/communities/{s.CommunityId}/architectural-settings", new
        {
            reviewPeriodDays = 45, lapseRule = "DeemedApproved", decisionRule = "MajorityOfMembers", reminderDays = 7,
            timeZoneId = "America/New_York", formalDisapprovalStatement = "Formal disapproval.",
            lapseRuleCitation = "§12.3 of the Declaration"
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("§12.3 of the Declaration", (await res.Content.ReadFromJsonAsync<ArcSettingsDto>(Json))!.LapseRuleCitation);
        Assert.Equal("§12.3 of the Declaration", await WithDbAsync(db =>
            db.CommunityArcSettings.Where(x => x.CommunityId == s.CommunityId).Select(x => x.LapseRuleCitation).SingleAsync()));
    }
}

using System.Net;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// T024 (US1) and T048 (US5) — GET /communities/{id}/architectural-applications.
/// </summary>
public class ApplicationsListEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    // US1-S1: "Given I am an active board member of a community with 4 open and 27 closed applications,
    // When I open Architectural Applications, Then the Open tab is selected, its label reads "Open · 4",
    // the Closed tab reads "Closed · 27", and the table lists exactly the 4 open applications."
    // US1-S2: "When I select the Closed tab, Then the table lists exactly the 27 closed applications and none of the open ones."
    [Fact]
    public async Task OpenAndClosedTabs_ListExactlyTheirApplications_WithStableCounts()
    {
        var s = await CreateScenarioAsync(3);
        var open = new List<Guid>();
        for (var i = 0; i < 4; i++) open.Add(await CreateApplicationAsync(s));
        var closed = new List<Guid>();
        for (var i = 0; i < 27; i++)
            closed.Add(await CreateApplicationAsync(s, new AppSpec
            {
                Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved
            }));
        await LoginAsAsync(s.Board[0]);

        var openTab = await ListAsync(s.CommunityId);
        Assert.Equal(4, openTab.Counts.Open);
        Assert.Equal(27, openTab.Counts.Closed);
        Assert.Equal(open.OrderBy(x => x), openTab.Items.Select(i => i.Id).OrderBy(x => x));
        Assert.Equal(4, openTab.Total);

        var closedTab = await ListAsync(s.CommunityId, "?status=closed&limit=100");
        Assert.Equal(closed.OrderBy(x => x), closedTab.Items.Select(i => i.Id).OrderBy(x => x));
        Assert.DoesNotContain(closedTab.Items, i => open.Contains(i.Id));
        Assert.Equal(4, closedTab.Counts.Open);
        Assert.Equal(27, closedTab.Counts.Closed);
    }

    // US1-S3: "Given an open application exists for "711 Keystone Park Dr #29" owned by "Praneeth Pattyam",
    // When I type "Keystone Park" or "Pattyam" into the search box, Then that application is listed and
    // applications whose address and owner do not match are not."
    [Theory]
    [InlineData("Keystone Park")]
    [InlineData("Pattyam")]
    [InlineData("pattyam")]
    public async Task Search_MatchesAddressOrOwner_CaseInsensitively(string term)
    {
        var s = await CreateScenarioAsync(3);
        var match = await CreateApplicationAsync(s);
        var otherProperty = await WithDbAsync(db => CreatePropertyAsync(db, s.CommunityId, address: "105 Mainline Station"));
        var other = await CreateApplicationAsync(s, new AppSpec { OwnerName = "Hasan Mehdi" }, otherProperty);
        await LoginAsAsync(s.Board[0]);

        var result = await ListAsync(s.CommunityId, $"?search={Uri.EscapeDataString(term)}");

        Assert.Contains(result.Items, i => i.Id == match);
        Assert.DoesNotContain(result.Items, i => i.Id == other);
        Assert.Equal(2, result.Counts.Open); // counts ignore search
    }

    // US1-S4: "Given an open application, When the table renders its row, Then the row shows the application
    // ID (for example ARC-1042), the property address with the owner's name under it, the project description,
    // the attachment count ("📎 3 files", or "none" when there are no attachments), the due date, and the board vote tally."
    [Fact]
    public async Task Row_CarriesIdAddressOwnerProjectAttachmentsDueDateAndTally()
    {
        var s = await CreateScenarioAsync(5);
        var due = new DateOnly(2026, 6, 27);
        var withFiles = await CreateApplicationAsync(s, new AppSpec
        {
            Number = 1042, Due = due,
            Attachments = [("fence-plan.pdf", 1_258_291), ("elevation.jpg", 860_160), ("plat-survey.pdf", 2_202_009)]
        });
        var noFiles = await CreateApplicationAsync(s, new AppSpec { Number = 1043 });
        await LoginAsAsync(s.Board[0]);

        var items = (await ListAsync(s.CommunityId)).Items;
        var row = items.Single(i => i.Id == withFiles);
        Assert.Equal("ARC-1042", row.DisplayId);
        Assert.Equal("711 Keystone Park Dr #29", row.PropertyAddress);
        Assert.Equal("Praneeth Pattyam", row.OwnerName);
        Assert.Equal("Fence replacement — 6ft cedar", row.ProjectTitle);
        Assert.Equal(3, row.AttachmentCount);
        Assert.Equal(due, row.DueDate);
        Assert.Equal(5, row.Tally.Eligible);
        Assert.Equal(0, items.Single(i => i.Id == noFiles).AttachmentCount);
    }

    // US1-S5: "Given 2 board members voted approve, 0 voted deny and 3 have not voted, When the row renders,
    // Then the tally shows 2 approve, 0 deny and 3 not voted, reads "2/5"…"
    [Fact]
    public async Task Tally_CountsVotesAndNotVoted()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[1], ArcVoteChoice.Approve), (s.Board[2], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[0]);

        var tally = (await ListAsync(s.CommunityId)).Items.Single().Tally;
        Assert.Equal(new Features.Board.Architectural.ArcTallyDto(2, 0, 0, 3, 5), tally);
    }

    // US1-S6: "Given 1 open application has no vote from me, When the page loads, Then the header shows a
    // pill reading "1 awaiting your vote"."
    [Fact]
    public async Task Counts_AwaitingMyVote_CountsOnlyUnvotedOpenApplications()
    {
        var s = await CreateScenarioAsync(3);
        await CreateApplicationAsync(s);
        var voted = await CreateApplicationAsync(s);
        await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        await AddVotesAsync(voted, (s.Board[0], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[0]);

        Assert.Equal(1, (await ListAsync(s.CommunityId)).Counts.AwaitingMyVote);
    }

    // US1-S7: "Given I am an active board member of community A only, When I request the applications for
    // community B, Then the request is refused and does not reveal whether community B exists or has applications."
    [Fact]
    public async Task OtherCommunity_AndNonexistentCommunity_AreRefusedIdentically()
    {
        var a = await CreateScenarioAsync(1);
        var b = await CreateScenarioAsync(1);
        await CreateApplicationAsync(b);
        await LoginAsAsync(a.Board[0]);

        var other = await Client.GetAsync(AppsUrl(b.CommunityId));
        var missing = await Client.GetAsync(AppsUrl(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(await other.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Accountant_IsRefused()
    {
        var s = await CreateScenarioAsync(1);
        var accountant = await CreateMemberAsync(s.CommunityId, CommunityRole.Accountant);
        await LoginAsAsync(accountant);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync(AppsUrl(s.CommunityId))).StatusCode);
    }

    [Fact]
    public async Task Paging_DefaultsTo25_ClampsTo100_AndSetsNoStore()
    {
        var s = await CreateScenarioAsync(1);
        for (var i = 0; i < 26; i++) await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var res = await Client.GetAsync(AppsUrl(s.CommunityId));
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
        var page = await ListAsync(s.CommunityId);
        Assert.Equal(25, page.Limit);
        Assert.Equal(25, page.Items.Count);
        Assert.Equal(26, page.Total);
        Assert.Equal(100, (await ListAsync(s.CommunityId, "?limit=101")).Limit); // repo Paging convention: clamp
    }

    [Theory]
    [InlineData("?status=pending")]
    public async Task InvalidStatus_Returns422(string query)
    {
        var s = await CreateScenarioAsync(1);
        await LoginAsAsync(s.Board[0]);
        var res = await Client.GetAsync(AppsUrl(s.CommunityId) + query);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ErrorCodeAsync(res));
    }

    // US5-S1: "Given 1 open application lacks my vote and 3 others already have it, When I land on Community Home,
    // Then the "Needs your vote" card shows "1 open" and lists only that application…"
    // US5-S3: "…When I click Approve on it, Then my vote is saved and the application leaves the card."
    // US5-S4: "Given I have voted on every open application… the card shows an empty state…"
    [Fact]
    public async Task AwaitingMyVote_ListsOnlyUnvoted_AndEmptiesAfterVoting()
    {
        var s = await CreateScenarioAsync(5);
        var pending = await CreateApplicationAsync(s);
        for (var i = 0; i < 3; i++)
        {
            var voted = await CreateApplicationAsync(s);
            await AddVotesAsync(voted, (s.Board[0], ArcVoteChoice.Approve));
        }
        await LoginAsAsync(s.Board[0]);

        var card = await ListAsync(s.CommunityId, "?awaitingMyVote=true");
        Assert.Equal(pending, Assert.Single(card.Items).Id);
        Assert.Equal(1, card.Counts.AwaitingMyVote);

        (await VoteAsync(s.CommunityId, pending, "Approve")).EnsureSuccessStatusCode();

        var after = await ListAsync(s.CommunityId, "?awaitingMyVote=true");
        Assert.Empty(after.Items);
        Assert.Equal(0, after.Counts.AwaitingMyVote);
    }

    // Edge case: a recused board member never sees the application in their "awaiting" feed.
    [Fact]
    public async Task AwaitingMyVote_ExcludesApplicationsIAmRecusedFrom()
    {
        var s = await CreateScenarioAsync(3);
        await CreateApplicationAsync(s);
        await LinkUserToPropertyAsync(s.Board[0].UserId, s.PropertyId);
        await LoginAsAsync(s.Board[0]);

        Assert.Empty((await ListAsync(s.CommunityId, "?awaitingMyVote=true")).Items);
    }
}

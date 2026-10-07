using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T044 (US4) — POST …/info-requests.</summary>
public class InfoRequestEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private Task<HttpResponseMessage> RequestInfoAsync(Guid c, Guid a, string? message) =>
        Client.PostAsJsonAsync($"{AppUrl(c, a)}/info-requests", new { message });

    // US4-S2: "When I enter "Please attach a plat survey" and click Request info, Then the application shows an
    // "info requested" marker with my message and name, the vote tally is unchanged, and my Approve and Deny
    // buttons are still available." FR-021: the due date doesn't move.
    [Fact]
    public async Task RequestInfo_MarksApplication_WithoutVotingOrMovingTheDueDate()
    {
        var s = await CreateScenarioAsync(5);
        var due = new DateOnly(2026, 6, 27);
        var appId = await CreateApplicationAsync(s, new AppSpec { Due = due });
        await AddVotesAsync(appId, (s.Board[1], ArcVoteChoice.Approve));
        await LoginAsAsync(s.Board[0]);

        var res = await RequestInfoAsync(s.CommunityId, appId, "Please attach a plat survey");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = (await res.Content.ReadFromJsonAsync<ArcInfoRequestCreatedDto>(Json))!;
        Assert.Equal(due, created.DueDate);
        Assert.Equal("Board Tester", created.RequestedBy);

        var row = (await ListAsync(s.CommunityId)).Items.Single();
        Assert.True(row.InfoRequested);
        Assert.Equal(new ArcTallyDto(1, 0, 0, 4, 5), row.Tally);
        Assert.Equal(ArcVoteStates.CanVote, row.MyVote.State);
        Assert.Equal(due, row.DueDate);
        var d = await DetailAsync(s.CommunityId, appId);
        Assert.Equal("Please attach a plat survey", Assert.Single(d.InfoRequests).Message);
        Assert.Equal(due, d.DueDate);
    }

    // US4-S3: "Given I click Request info with an empty message, When I submit, Then the request is refused with a
    // message saying what information is needed."
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmptyMessage_IsRefused(string? message)
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);

        var res = await RequestInfoAsync(s.CommunityId, appId, message);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
        Assert.Contains("information is needed", body);
    }

    // US4-S4: "Given a board member requested info, When the community manager views the application, Then they
    // see the request, its message, who sent it and when."
    [Fact]
    public async Task Manager_SeesTheRequestWithSenderAndTime()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);
        (await RequestInfoAsync(s.CommunityId, appId, "Please attach a plat survey")).EnsureSuccessStatusCode();

        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);
        var request = Assert.Single((await DetailAsync(s.CommunityId, appId)).InfoRequests);

        Assert.Equal("Please attach a plat survey", request.Message);
        Assert.Equal("Board Tester", request.RequestedBy);
        Assert.NotEqual(default, request.RequestedAt);
    }

    [Theory]
    [InlineData(CommunityRole.CommunityManager)]
    [InlineData(CommunityRole.Accountant)]
    public async Task NonBoardRoles_CannotRequestInfo(CommunityRole role)
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(await CreateMemberAsync(s.CommunityId, role));
        Assert.Equal(HttpStatusCode.Forbidden, (await RequestInfoAsync(s.CommunityId, appId, "x")).StatusCode);
    }

    [Fact]
    public async Task ClosedApplication_IsRefused()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        await LoginAsAsync(s.Board[0]);

        var res = await RequestInfoAsync(s.CommunityId, appId, "x");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(ArcErrorCodes.ApplicationClosed, await ErrorCodeAsync(res));
    }
}

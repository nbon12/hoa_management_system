using System.Net.Http.Json;
using System.Net;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// T053 — SC-005: "Every application that meets its community's decision rule shows exactly one decision,
/// including when the deciding votes arrive simultaneously." FR-018: votes after a decision are refused.
/// </summary>
public class ArcConcurrencyTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    [Fact]
    public async Task SimultaneousDecidingVotes_ProduceExactlyOneDecision()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve), (s.Board[1], ArcVoteChoice.Approve));

        var tokenA = await LoginAsync(s.Board[2].Email);
        var tokenB = await LoginAsync(s.Board[3].Email);
        using var a = ClientWithToken(tokenA);
        using var b = ClientWithToken(tokenB);
        var url = $"{AppUrl(s.CommunityId, appId)}/votes";

        var responses = await Task.WhenAll(
            a.PostAsJsonAsync(url, new { choice = "Approve" }),
            b.PostAsJsonAsync(url, new { choice = "Approve" }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var refused = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(ArcErrorCodes.ApplicationDecided, await ErrorCodeAsync(refused));

        var app = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().SingleAsync(x => x.Id == appId));
        Assert.Equal(ArcApplicationStatus.DecisionReached, app.Status);
        Assert.Equal(ArcOutcome.Approved, app.DecisionOutcome);
        Assert.NotNull(app.DecisionReachedAt);
        Assert.Equal(3, await WithDbAsync(db => db.ArchitecturalVotes.CountAsync(v => v.ApplicationId == appId)));

        // Recording the outcome then yields exactly one owner email row.
        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);
        (await Client.PostAsJsonAsync($"{AppUrl(s.CommunityId, appId)}/outcome", new { })).EnsureSuccessStatusCode();
        Assert.Equal(1, await WithDbAsync(db => db.OutboxMessages.CountAsync(m => m.DedupKey == $"arc:{appId}:outcome")));
    }
}

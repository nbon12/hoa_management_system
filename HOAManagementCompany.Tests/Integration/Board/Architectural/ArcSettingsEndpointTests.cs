using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T058 (US6) — per-community ARC settings (FR-029/FR-030).</summary>
public class ArcSettingsEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private static string Url(Guid c) => $"/api/v1/communities/{c}/architectural-settings";

    private static object Body(int review = 45, string lapse = "DeemedApproved", string rule = "MajorityOfVotesCastWithQuorum",
        int reminder = 5, string tz = "America/Chicago", string statement = "Formal disapproval under §12.3 of the Declaration.") =>
        new { reviewPeriodDays = review, lapseRule = lapse, decisionRule = rule, reminderDays = reminder, timeZoneId = tz, formalDisapprovalStatement = statement };

    private async Task<Guid> CreateViaFactoryAsync(ArcScenario s, DateOnly received)
    {
        using var scope = NewScope();
        var app = await scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>().CreateFromSettingsAsync(
            new ArcNewApplication(s.PropertyId, "Owner", ArcProjectType.Fence, "Fence", "", received, null, []), default);
        await Db(scope).SaveChangesAsync();
        return app.Id;
    }

    // US6-S17: "Given a community with a review period of 45 days, When an application received on 05/01/26 is shown,
    // Then its due date is 06/15/26."
    // US6-S18: "Given I am a community manager, When I set my community's review period, lapse rule, decision rule,
    // reminder days and formal disapproval statement, Then the new values are saved and apply to applications received
    // afterwards. A board member or resident who tries to change them is refused."
    [Fact]
    public async Task ManagerSavesSettings_NewApplicationsUseThem_OldOnesKeepSnapshots()
    {
        var s = await CreateScenarioAsync(1);
        var before = await CreateViaFactoryAsync(s, new DateOnly(2026, 4, 1));
        await LoginAsAsync(await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager));

        var res = await Client.PutAsJsonAsync(Url(s.CommunityId), Body());
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var saved = (await res.Content.ReadFromJsonAsync<ArcSettingsDto>(Json))!;
        Assert.Equal(45, saved.ReviewPeriodDays);
        Assert.Equal("DeemedApproved", saved.LapseRule);
        Assert.Equal("MajorityOfVotesCastWithQuorum", saved.DecisionRule);
        Assert.Equal(5, saved.ReminderDays);
        Assert.Equal("America/Chicago", saved.TimeZoneId);

        var after = await CreateViaFactoryAsync(s, new DateOnly(2026, 5, 1));
        var apps = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking()
            .Where(a => a.Id == before || a.Id == after).ToDictionaryAsync(a => a.Id));
        Assert.Equal(new DateOnly(2026, 6, 15), apps[after].DueDate);
        Assert.Equal(ArcLapseRule.DeemedApproved, apps[after].LapseRule);
        Assert.Equal(new DateOnly(2026, 5, 1), apps[before].DueDate);           // 30-day default kept
        Assert.Equal(ArcLapseRule.FlagOverdueOnly, apps[before].LapseRule);
        Assert.Equal(ArcDecisionRule.MajorityOfMembers, apps[before].DecisionRule);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcSettingsChanged"));
    }

    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Resident)]
    [InlineData(CommunityRole.Accountant)]
    public async Task NonManagers_CannotChangeSettings(CommunityRole role)
    {
        var s = await CreateScenarioAsync(1);
        await LoginAsAsync(await CreateMemberAsync(s.CommunityId, role));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PutAsJsonAsync(Url(s.CommunityId), Body())).StatusCode);
    }

    public static TheoryData<object> InvalidBodies => new()
    {
        Body(review: 0), Body(review: 366), Body(reminder: -1), Body(reminder: 31), Body(tz: "Mars/Olympus"),
        Body(statement: "  "), Body(statement: new string('x', 1001)), Body(lapse: "Sometimes"), Body(rule: "1"),
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidSettings_Return422(object body)
    {
        var s = await CreateScenarioAsync(1);
        await LoginAsAsync(await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager));
        var res = await Client.PutAsJsonAsync(Url(s.CommunityId), body);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Get_WithNoRow_ReturnsDefaults_WithoutWriting()
    {
        var s = await CreateScenarioAsync(1);
        await LoginAsAsync(s.Board[0]);

        var dto = (await Client.GetFromJsonAsync<ArcSettingsDto>(Url(s.CommunityId), Json))!;

        Assert.Equal(new ArcSettingsDto(30, "FlagOverdueOnly", "MajorityOfMembers", 7, "America/New_York",
            Domain.Entities.CommunityArcSettings.DefaultFormalDisapprovalStatement, null), dto);
        Assert.False(await WithDbAsync(db => db.CommunityArcSettings.AnyAsync(x => x.CommunityId == s.CommunityId)));
    }
}

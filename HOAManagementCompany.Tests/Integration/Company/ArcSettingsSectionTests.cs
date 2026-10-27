using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T129 — the Architectural review section of the community settings page (US5; FR-027,
/// FR-030, FR-031; contracts/community-settings.md § Architectural review section).
/// </summary>
public class ArcSettingsSectionTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private static string Url(Guid community) => $"/api/v1/communities/{community}/architectural-settings";

    private static object Body(int reviewPeriodDays = 45, string lapseRule = "FlagOverdueOnly") => new
    {
        reviewPeriodDays, lapseRule, decisionRule = "MajorityOfMembers", reminderDays = 7, timeZoneId = "America/New_York",
        formalDisapprovalStatement = CommunityArcSettings.DefaultFormalDisapprovalStatement,
    };

    // US5-S1
    [Fact]
    public async Task ACompanyAdministratorWithNoMembership_EditsAndResetsTheRules_ButCantReviewApplications()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var app = await CreateArcApplicationAsync(community, await CreatePropertyAsync(community), 1001);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var get = await admin.GetAsync(Url(community));
        var put = await admin.PutAsJsonAsync(Url(community), Body());
        var reset = await admin.PostAsync($"{Url(community)}/reset", null);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (get.StatusCode, put.StatusCode, reset.StatusCode));
        Assert.True((await JsonAsync(get)).GetProperty("canEdit").GetBoolean());

        var list = await admin.GetAsync($"/api/v1/communities/{community}/architectural-applications");
        var vote = await admin.PostAsJsonAsync($"/api/v1/communities/{community}/architectural-applications/{app}/votes",
            new { choice = "Approve" });
        foreach (var res in new[] { list, vote })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
    }

    // US5-S2, US5-S4
    [Fact]
    public async Task TheManagerEditsTheRules_EachSaveIsLogged_AndABoardMemberOnlyReadsThem()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var (_, boardEmail) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        using var manager = await ClientForAsync(managerEmail);
        using var board = await ClientForAsync(boardEmail);

        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync(Url(community), Body(reviewPeriodDays: 45))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync(Url(community), Body(reviewPeriodDays: 60))).StatusCode);

        var changes = LogSink.Events.Where(e => e.MessageTemplate.Text.StartsWith("ArcSettingsChanged")
                                                && e.Properties["CommunityId"].ToString() == community.ToString()).ToList();
        Assert.Equal(2, changes.Count);
        Assert.All(changes, e => Assert.Equal($"\"{managerId}\"", e.Properties["ActorId"].ToString()));
        Assert.Contains("\"ReviewPeriodDays\":30", Raw(changes[0], "OldSettings"));
        Assert.Contains("\"ReviewPeriodDays\":45", Raw(changes[0], "NewSettings"));
        Assert.Contains("\"ReviewPeriodDays\":60", Raw(changes[1], "NewSettings"));

        var read = await board.GetAsync(Url(community));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var body = await JsonAsync(read);
        Assert.Equal((60, false), (body.GetProperty("reviewPeriodDays").GetInt32(), body.GetProperty("canEdit").GetBoolean()));
        var save = await board.PutAsJsonAsync(Url(community), Body(reviewPeriodDays: 90));
        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(save)).GetProperty("code").GetString());
    }

    // US5-S5, and the GET's companyDefaults and differsFromDefaults
    [Fact]
    public async Task ResettingToTheDefaults_CopiesTheSixRulesOnly()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync(Url(community), Body(reviewPeriodDays: 45))).StatusCode);
        var received = await CreateFromSettingsAsync(home);
        var before = await JsonAsync(await manager.GetAsync(Url(community)));
        Assert.Equal(30, before.GetProperty("companyDefaults").GetProperty("reviewPeriodDays").GetInt32());
        Assert.Equal(["reviewPeriodDays"], before.GetProperty("differsFromDefaults").EnumerateArray().Select(f => f.GetString()));
        var nextNumber = await NextNumberAsync(community);

        var res = await manager.PostAsync($"{Url(community)}/reset", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Empty(body.GetProperty("differsFromDefaults").EnumerateArray());
        var settings = await WithDbAsync(db => db.CommunityArcSettings.AsNoTracking().SingleAsync(s => s.CommunityId == community));
        var defaults = new CompanyDefaultSettings();
        Assert.False(defaults.DiffersFrom(settings));
        Assert.Equal(nextNumber, settings.NextApplicationNumber);

        // The application already received keeps its 45-day snapshot; the next one gets the next number.
        var kept = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().SingleAsync(a => a.Id == received.Id));
        Assert.Equal(kept.ReceivedDate.AddDays(45), kept.DueDate);
        var next = await CreateFromSettingsAsync(home);
        Assert.Equal((nextNumber, next.ReceivedDate.AddDays(30)), (next.ApplicationNumber, next.DueDate));

        var logged = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcSettingsChanged")
                                                        && e.Properties["Cause"].ToString() == "\"reset\"");
        Assert.Contains("\"ReviewPeriodDays\":45", Raw(logged, "OldSettings"));
        Assert.Contains("\"ReviewPeriodDays\":30", Raw(logged, "NewSettings"));
    }

    // US5-S7
    [Fact]
    public async Task InAnArchivedCommunity_TheAdministratorReadsEverySection_AndCanOnlyRestoreOrEnd()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        await CreateResidentAsync(await CreatePropertyAsync(community));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        using (var manager = await ClientForAsync(managerEmail))
            Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
                new { role = (int)CommunityRole.BoardMember, email = $"pat-{Guid.NewGuid():N}@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);

        foreach (var path in new[] { "settings", "profile", "memberships", "invitations", "residents", "architectural-settings" })
        {
            var res = await admin.GetAsync($"/api/v1/communities/{community}/{path}");
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{path}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        var invitations = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/invitations"));
        var revoked = Assert.Single(invitations.GetProperty("items").EnumerateArray());
        Assert.Equal(("Revoked", "CommunityArchived"),
            (revoked.GetProperty("state").GetString(), revoked.GetProperty("revokeReason").GetString()));

        foreach (var write in new[]
                 {
                     await admin.PutAsJsonAsync(Url(community), Body()),
                     await admin.PostAsync($"{Url(community)}/reset", null),
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, write.StatusCode);
            Assert.Equal("COMMUNITY_ARCHIVED", (await JsonAsync(write)).GetProperty("code").GetString());
        }
        Assert.False((await JsonAsync(await admin.GetAsync(Url(community)))).GetProperty("canEdit").GetBoolean());

        var can = (await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/settings"))).GetProperty("can");
        var offered = can.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.True
                        || (p.Value.ValueKind == JsonValueKind.Object && p.Value.GetProperty("allowed").GetBoolean()))
            .Select(p => p.Name).Order().ToList();
        Assert.Equal(["endMembershipsWhileArchived", "restore"], offered);
    }

    private async Task<ArchitecturalApplication> CreateFromSettingsAsync(Guid home)
    {
        using var scope = NewScope();
        var app = await scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>().CreateFromSettingsAsync(
            new ArcNewApplication(home, "Pat Owner", ArcProjectType.Fence, "Fence", "", DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime),
                null, []), default);
        await Db(scope).SaveChangesAsync();
        return app;
    }

    private static string Raw(Serilog.Events.LogEvent e, string name) =>
        (string)((Serilog.Events.ScalarValue)e.Properties[name]).Value!;

    private Task<int> NextNumberAsync(Guid community) => WithDbAsync(db =>
        db.CommunityArcSettings.Where(s => s.CommunityId == community).Select(s => s.NextApplicationNumber).SingleAsync());
}

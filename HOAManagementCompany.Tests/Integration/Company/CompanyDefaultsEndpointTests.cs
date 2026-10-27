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
/// 030 T124 — company-wide defaults that new communities copy (US4; FR-025, FR-026, R13;
/// contracts/company.md § Company defaults).
/// </summary>
public class CompanyDefaultsEndpointTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Url = "/api/v1/company/defaults";

    private static object Defaults(string lapseRule = "FlagOverdueOnly", int reviewPeriodDays = 30, int reminderDays = 7,
        string decisionRule = "MajorityOfMembers", string timeZoneId = "America/New_York",
        string statement = "The board does not approve this application.") => new
    {
        reviewPeriodDays, lapseRule, decisionRule, reminderDays, timeZoneId, formalDisapprovalStatement = statement,
    };

    // US4-S1
    [Fact]
    public async Task ANewCommunity_CopiesTheChangedDefault_AndExistingOnesKeepTheirs()
    {
        var companyId = await CreateCompanyAsync();
        var existing = await CreateCommunityInAsync(companyId);
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var put = await admin.PutAsJsonAsync(Url, Defaults(lapseRule: "DeemedApproved"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var body = await JsonAsync(put);
        Assert.Equal(("DeemedApproved", true), (body.GetProperty("lapseRule").GetString(), body.GetProperty("canEdit").GetBoolean()));
        Assert.Equal(Clock.UtcNow, body.GetProperty("updatedAt").GetDateTimeOffset());

        var created = await admin.PostAsJsonAsync("/api/v1/communities", new
        {
            legalName = "Keystone Glen HOA, Inc.", communityName = $"Keystone Glen {Guid.NewGuid():N}"[..22], county = "Wake",
            formationDate = "2004-03-14", managementStartDate = "2026-11-01",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var newId = (await JsonAsync(created)).GetProperty("id").GetGuid();

        Assert.Equal(ArcLapseRule.DeemedApproved, await LapseRuleAsync(newId));
        Assert.Equal(ArcLapseRule.FlagOverdueOnly, await LapseRuleAsync(existing));

        // Logged with old and new values.
        var logged = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyDefaultsChanged"));
        Assert.Equal(($"\"{adminId}\"", companyId.ToString()), (logged.Properties["ActorId"].ToString(), logged.Properties["CompanyId"].ToString()));
        Assert.Contains("FlagOverdueOnly", logged.Properties["Old"].ToString());
        Assert.Contains("DeemedApproved", logged.Properties["New"].ToString());
    }

    // US4-S2, including a community whose ARC settings were never saved (FR-026)
    [Fact]
    public async Task ChangingEveryDefault_LeavesExistingCommunitiesAndTheirApplicationsUntouched()
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var created = await admin.PostAsJsonAsync("/api/v1/communities", new
        {
            legalName = "Keystone Glen HOA, Inc.", communityName = $"Keystone Glen {Guid.NewGuid():N}"[..22], county = "Wake",
            formationDate = "2004-03-14", managementStartDate = "2026-11-01",
        });
        var bySpec = (await JsonAsync(created)).GetProperty("id").GetGuid();
        // Settings as the migration backfill wrote them.
        var backfilled = await CreateCommunityInAsync(companyId);
        // Settings never saved: no row, so it reads 027's built-in defaults.
        var neverSaved = await CreateCommunityInAsync(companyId);
        await WithDbAsync(db => db.CommunityArcSettings.Where(s => s.CommunityId == neverSaved).ExecuteDeleteAsync());
        Guid[] communities = [bySpec, backfilled, neverSaved];
        var apps = new List<Guid>();
        foreach (var community in communities)
            apps.Add(await CreateArcApplicationAsync(community, await CreatePropertyAsync(community), 1001));
        var before = await SnapshotAsync(communities, apps);
        var neverSavedBefore = await RulesAsync(admin, neverSaved);

        var put = await admin.PutAsJsonAsync(Url, Defaults(lapseRule: "DeemedDenied", reviewPeriodDays: 60, reminderDays: 14,
            decisionRule: "MajorityOfVotesCastWithQuorum", timeZoneId: "America/Chicago", statement: "Not approved."));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        // Also proves no row was written for the never-saved community: it would show in the snapshot.
        Assert.Equal(before, await SnapshotAsync(communities, apps));
        var builtIn = (30, "FlagOverdueOnly", "MajorityOfMembers", 7, "America/New_York",
            CommunityArcSettings.DefaultFormalDisapprovalStatement, JsonValueKind.Null);
        Assert.Equal(builtIn, neverSavedBefore);
        Assert.Equal(builtIn, await RulesAsync(admin, neverSaved));
        // The row made when it is first needed (its next application) holds 027's defaults, not the changed ones.
        using var scope = NewScope();
        var row = await scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>().GetOrCreateSettingsAsync(neverSaved, default);
        Assert.Equal((30, ArcLapseRule.FlagOverdueOnly, ArcDecisionRule.MajorityOfMembers, 7, "America/New_York",
                CommunityArcSettings.DefaultFormalDisapprovalStatement),
            (row.ReviewPeriodDays, row.LapseRule, row.DecisionRule, row.ReminderDays, row.TimeZoneId, row.FormalDisapprovalStatement));
    }

    // US4-S3
    [Fact]
    public async Task ACommunityManager_ReadsTheDefaults_ButCantChangeThem()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var get = await manager.GetAsync(Url);
        var put = await manager.PutAsJsonAsync(Url, Defaults(lapseRule: "DeemedApproved"));

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await JsonAsync(get);
        Assert.Equal(("FlagOverdueOnly", false), (body.GetProperty("lapseRule").GetString(), body.GetProperty("canEdit").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("updatedAt").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(put)).GetProperty("code").GetString());
        Assert.Equal(ArcLapseRule.FlagOverdueOnly,
            await WithDbAsync(db => db.CompanyDefaultSettings.Where(d => d.CompanyId == companyId).Select(d => d.LapseRule).SingleAsync()));
    }

    // US4-S4
    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Accountant)]
    [InlineData(CommunityRole.Resident)]
    public async Task OtherRoles_CantReadTheDefaults(CommunityRole role)
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var email = role == CommunityRole.Resident
            ? (await CreateResidentAsync(await CreatePropertyAsync(community))).Email
            : (await CreateStaffAsync(community, role)).Email;
        using var client = await ClientForAsync(email);

        var res = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
    }

    // US4-S5
    [Theory]
    [InlineData("reviewPeriodDays", 0, 7, 10, "between 1 and 365")]
    [InlineData("reviewPeriodDays", 400, 7, 10, "between 1 and 365")]
    [InlineData("reminderDays", 30, 31, 10, "between 0 and 30")]
    [InlineData("formalDisapprovalStatement", 30, 7, 1001, "at most 1000 characters")]
    public async Task AnOutOfRangeValue_IsRefused_NamingTheAllowedRange(string field, int reviewPeriodDays, int reminderDays,
        int statementLength, string range)
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PutAsJsonAsync(Url, Defaults(reviewPeriodDays: reviewPeriodDays, reminderDays: reminderDays,
            statement: new string('a', statementLength)));

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(field, error.GetProperty("field").GetString());
        Assert.Contains(range, error.GetProperty("message").GetString());
        Assert.Null(await WithDbAsync(db => db.CompanyDefaultSettings.Where(d => d.CompanyId == companyId)
            .Select(d => d.UpdatedByUserId).SingleOrDefaultAsync()));
    }

    // contracts/company.md § GET /company/defaults: a company with no defaults row yet reads the 027
    // defaults, and the row is created so the next PUT has one to change.
    [Fact]
    public async Task ACompanyWithNoDefaultsRow_ReadsThe027Defaults_AndTheRowIsCreated()
    {
        var companyId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.ManagementCompanies.Add(new Domain.Entities.ManagementCompany { Id = companyId, Name = $"No Defaults {companyId:N}" });
            await db.SaveChangesAsync();
        });
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal((30, "FlagOverdueOnly", "MajorityOfMembers", 7, "America/New_York",
                "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit.",
                JsonValueKind.Null, true),
            (body.GetProperty("reviewPeriodDays").GetInt32(), body.GetProperty("lapseRule").GetString(),
             body.GetProperty("decisionRule").GetString(), body.GetProperty("reminderDays").GetInt32(),
             body.GetProperty("timeZoneId").GetString(), body.GetProperty("formalDisapprovalStatement").GetString(),
             body.GetProperty("updatedAt").ValueKind, body.GetProperty("canEdit").GetBoolean()));
        var row = await WithDbAsync(db => db.CompanyDefaultSettings.AsNoTracking().SingleAsync(d => d.CompanyId == companyId));
        Assert.Equal((30, ArcLapseRule.FlagOverdueOnly, (string?)null), (row.ReviewPeriodDays, row.LapseRule, row.UpdatedByUserId));
    }

    // The six rules and updatedAt as the community's Architectural review section reads them.
    private static async Task<(int, string?, string?, int, string?, string?, JsonValueKind)> RulesAsync(HttpClient client, Guid community)
    {
        var res = await client.GetAsync($"/api/v1/communities/{community}/architectural-settings");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        return (body.GetProperty("reviewPeriodDays").GetInt32(), body.GetProperty("lapseRule").GetString(),
            body.GetProperty("decisionRule").GetString(), body.GetProperty("reminderDays").GetInt32(),
            body.GetProperty("timeZoneId").GetString(), body.GetProperty("formalDisapprovalStatement").GetString(),
            body.GetProperty("updatedAt").ValueKind);
    }

    private Task<ArcLapseRule> LapseRuleAsync(Guid community) =>
        WithDbAsync(db => db.CommunityArcSettings.Where(s => s.CommunityId == community).Select(s => s.LapseRule).SingleAsync());

    private Task<string> SnapshotAsync(Guid[] communities, List<Guid> apps) => WithDbAsync(async db =>
    {
        var settings = await db.CommunityArcSettings.AsNoTracking().Where(s => communities.Contains(s.CommunityId))
            .OrderBy(s => s.CommunityId).ToListAsync();
        var applications = await db.ArchitecturalApplications.AsNoTracking().Where(a => apps.Contains(a.Id)).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.DecisionRule, a.LapseRule, a.TimeZoneId, a.ReceivedDate, a.DueDate }).ToListAsync();
        return JsonSerializer.Serialize(settings.Select(s => new
        {
            s.CommunityId, s.ReviewPeriodDays, s.LapseRule, s.DecisionRule, s.ReminderDays, s.TimeZoneId,
            s.FormalDisapprovalStatement, s.UpdatedAt,
        })) + JsonSerializer.Serialize(applications);
    });
}

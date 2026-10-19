using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
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

    // US4-S2
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
        var apps = new List<Guid>();
        foreach (var community in new[] { bySpec, backfilled })
            apps.Add(await CreateArcApplicationAsync(community, await CreatePropertyAsync(community), 1001));
        var before = await SnapshotAsync([bySpec, backfilled], apps);

        var put = await admin.PutAsJsonAsync(Url, Defaults(lapseRule: "DeemedDenied", reviewPeriodDays: 60, reminderDays: 14,
            decisionRule: "MajorityOfVotesCastWithQuorum", timeZoneId: "America/Chicago", statement: "Not approved."));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(before, await SnapshotAsync([bySpec, backfilled], apps));
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

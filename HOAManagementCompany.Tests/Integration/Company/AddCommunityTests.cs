using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T071 — adding a community (US1; FR-006–FR-009, FR-016; contracts/company.md § POST /communities).
/// </summary>
public class AddCommunityTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US1-S1
    [Fact]
    public async Task CompanyAdministrator_AddsACommunity_OnboardingWithTheDefaultsAndNoManager()
    {
        var companyId = await CreateCompanyAsync();
        await WithDbAsync(db => db.CompanyDefaultSettings.Where(d => d.CompanyId == companyId).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.ReviewPeriodDays, 30)
            .SetProperty(d => d.LapseRule, ArcLapseRule.DeemedApproved)
            .SetProperty(d => d.DecisionRule, ArcDecisionRule.MajorityOfVotesCastWithQuorum)
            .SetProperty(d => d.ReminderDays, 5)
            .SetProperty(d => d.TimeZoneId, "America/Chicago")
            .SetProperty(d => d.FormalDisapprovalStatement, "Juniper Row's disapproval statement.")));
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);
        var name = $"Keystone Park {Guid.NewGuid():N}"[..40];

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body(name));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);
        var body = await JsonAsync(res);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal((name, "Onboarding", $"/app/company/communities/{id}/settings/profile"),
            (body.GetProperty("communityName").GetString(), body.GetProperty("status").GetString(),
             body.GetProperty("settingsPath").GetString()));

        await WithDbAsync(async db =>
        {
            var community = await db.Communities.SingleAsync(c => c.Id == id);
            Assert.Equal((CommunityStatus.Onboarding, (Guid?)null, companyId, "Keystone Park HOA, Inc.", "Hamilton",
                    new DateOnly(2004, 3, 14), new DateOnly(2026, 11, 1)),
                (community.Status, community.ParentCommunityId, community.ManagementCompanyId, community.LegalName,
                 community.County, community.FormationDate, community.ManagementStartDate));
            Assert.Equal(0, await db.CommunityMemberships.CountAsync(m => m.CommunityId == id && m.Role == CommunityRole.CommunityManager));
            var settings = await db.CommunityArcSettings.SingleAsync(s => s.CommunityId == id);
            Assert.Equal((30, ArcLapseRule.DeemedApproved, ArcDecisionRule.MajorityOfVotesCastWithQuorum, 5, "America/Chicago",
                    "Juniper Row's disapproval statement."),
                (settings.ReviewPeriodDays, settings.LapseRule, settings.DecisionRule, settings.ReminderDays,
                 settings.TimeZoneId, settings.FormalDisapprovalStatement));
        });

        var portfolio = await JsonAsync(await client.GetAsync("/api/v1/company/communities?limit=100"));
        Assert.Contains(portfolio.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityLifecycle")
            && e.Properties["CommunityId"].ToString() == id.ToString()
            && e.Properties["Action"].ToString() == "\"Created\""
            && !e.RenderMessage().Contains(name));
    }

    // US1-S2: names are unique in the company, ignoring case and outer spaces.
    [Fact]
    public async Task CompanyAdministrator_AddingATakenName_IsRefused_NamingTheOtherCommunity()
    {
        var companyId = await CreateCompanyAsync();
        var name = $"Keystone Park {Guid.NewGuid():N}"[..30];
        await CreateCommunityInAsync(companyId, name: name);
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);
        var before = await CountAsync(companyId);

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body(name.ToLowerInvariant() + " "));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("COMMUNITY_NAME_TAKEN", body.GetProperty("code").GetString());
        Assert.Contains(name, body.GetProperty("message").GetString());
        Assert.Equal(before, await CountAsync(companyId));
    }

    // US1-S3: anyone else learns only that the name is unavailable.
    [Fact]
    public async Task CommunityManager_AddingATakenName_IsToldOnlyThatItIsUnavailable()
    {
        var companyId = await CreateCompanyAsync();
        var name = $"Keystone Park {Guid.NewGuid():N}"[..30];
        var taken = await CreateCommunityInAsync(companyId, name: name);
        var (_, email) = await CreateManagerAsync(await CreateCommunityInAsync(companyId));
        using var client = await ClientForAsync(email);
        var before = await CountAsync(companyId);

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body(name));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Equal("That community name is unavailable.", JsonDocument.Parse(text).RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain(name, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(taken.ToString(), text);
        Assert.Equal(before, await CountAsync(companyId));
    }

    // US1-S4
    [Fact]
    public async Task CommunityManager_AddsACommunity_BecomesItsManager_AndEveryAdministratorIsEmailed()
    {
        var companyId = await CreateCompanyAsync();
        var (managerId, email) = await CreateManagerAsync(await CreateCommunityInAsync(companyId));
        var (adminA, _) = await CreateCompanyAdminAsync(companyId);
        var (adminB, _) = await CreateCompanyAdminAsync(companyId);
        var (otherCompanyAdmin, _) = await CreateCompanyAdminAsync(await CreateCompanyAsync());
        using var client = await ClientForAsync(email);

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body("Keystone Ridge " + Guid.NewGuid().ToString("N")[..8]));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await JsonAsync(res);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal("/app/board/settings/profile", body.GetProperty("settingsPath").GetString());
        await WithDbAsync(async db =>
        {
            Assert.Equal(CommunityStatus.Onboarding, await db.Communities.Where(c => c.Id == id).Select(c => c.Status).SingleAsync());
            var membership = await db.CommunityMemberships.SingleAsync(m => m.CommunityId == id);
            Assert.Equal((managerId, CommunityRole.CommunityManager, MembershipStatus.Active, (DateOnly?)null),
                (membership.UserId, membership.Role, membership.Status, membership.EndDate));

            var notices = await db.OutboxMessages.Where(m => m.Kind == CompanyEmailKinds.CommunityAdded
                                                          && m.DedupKey!.Contains(id.ToString())).ToListAsync();
            Assert.Equal(new[] { adminA, adminB }.Order(), notices.Select(n => n.RecipientUserId!).Order());
            Assert.DoesNotContain(notices, n => n.RecipientUserId == otherCompanyAdmin || n.RecipientUserId == managerId);
            Assert.All(notices, n => Assert.Contains($"/app/company/communities/{id}/settings/profile", n.PayloadJson));
        });

        var mine = await JsonAsync(await client.GetAsync("/api/v1/me/communities"));
        Assert.Contains(mine.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
    }

    // US1-S5
    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Accountant)]
    [InlineData(CommunityRole.Resident)]
    [InlineData(null)]
    public async Task WithoutTheRight_IsForbidden_AndNothingIsCreated(CommunityRole? role)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var email = role switch
        {
            null => EmailOf(await WithDbAsync(CreateUserAsync)),
            CommunityRole.Resident => (await CreateResidentAsync(await CreatePropertyAsync(community))).Email,
            { } r => (await CreateStaffAsync(community, r)).Email,
        };
        using var client = await ClientForAsync(email);
        var name = "Keystone Park " + Guid.NewGuid().ToString("N")[..8];

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body(name));

        await AssertForbiddenAsync(res);
        Assert.False(await WithDbAsync(db => db.Communities.AnyAsync(c => c.CommunityName == name)));
    }

    // US1-S6
    [Fact]
    public async Task AManagerOfOnlyAnArchivedCommunity_IsForbidden()
    {
        var companyId = await CreateCompanyAsync();
        var (_, email) = await CreateManagerAsync(await CreateCommunityInAsync(companyId, CommunityStatus.Archived));
        using var client = await ClientForAsync(email);
        var name = "Keystone Park " + Guid.NewGuid().ToString("N")[..8];

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body(name));

        await AssertForbiddenAsync(res);
        Assert.False(await WithDbAsync(db => db.Communities.AnyAsync(c => c.CommunityName == name)));
    }

    // US1-S7: a parent is a link, never a grant.
    [Fact]
    public async Task CompanyAdministrator_AddsASubAssociation_AndTheMastersBoardGetsNoAccess()
    {
        var companyId = await CreateCompanyAsync();
        var master = await CreateCommunityInAsync(companyId, name: "Keystone Crossing Master " + Guid.NewGuid().ToString("N")[..6]);
        var (_, boardEmail) = await CreateStaffAsync(master, CommunityRole.BoardMember);
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var res = await client.PostAsJsonAsync("/api/v1/communities",
            Body("Keystone Crossing SF " + Guid.NewGuid().ToString("N")[..6], parent: master));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (await JsonAsync(res)).GetProperty("id").GetGuid();
        Assert.Equal(master, await WithDbAsync(db => db.Communities.Where(c => c.Id == id).Select(c => c.ParentCommunityId).SingleAsync()));
        using var board = await ClientForAsync(boardEmail);
        await AssertForbiddenAsync(await board.GetAsync($"/api/v1/board/metrics?communityId={id}&surface=CommunityMetrics"));
    }

    // US1-S8: a manager may only choose a parent they manage, and can't tell whether one exists.
    [Fact]
    public async Task CommunityManager_ChoosingAParentTheyDontManage_IsForbidden_LikeANonexistentParent()
    {
        var companyId = await CreateCompanyAsync();
        var a = await CreateCommunityInAsync(companyId);
        var b = await CreateCommunityInAsync(companyId);
        var (_, email) = await CreateManagerAsync(a);
        using var client = await ClientForAsync(email);
        var name = "Keystone Park " + Guid.NewGuid().ToString("N")[..8];

        var real = await client.PostAsJsonAsync("/api/v1/communities", Body(name, parent: b));
        var missing = await client.PostAsJsonAsync("/api/v1/communities", Body(name, parent: Guid.NewGuid()));

        await AssertForbiddenAsync(real);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await real.Content.ReadAsStringAsync());
        Assert.Equal(missing.StatusCode, real.StatusCode);
        Assert.False(await WithDbAsync(db => db.Communities.AnyAsync(c => c.CommunityName == name)));

        // Their own community is a valid parent.
        var own = await client.PostAsJsonAsync("/api/v1/communities", Body(name, parent: a));
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
    }

    public static TheoryData<string, string[]> InvalidBodies => new()
    {
        { """{"legalName":"","communityName":"Keystone Park","county":"Hamilton","formationDate":"2004-03-14","managementStartDate":"2026-11-01"}""", ["legalName"] },
        { """{"legalName":"Keystone Park HOA, Inc.","communityName":" ","county":"Hamilton","formationDate":"2004-03-14","managementStartDate":"2026-11-01"}""", ["communityName"] },
        { """{"legalName":"Keystone Park HOA, Inc.","communityName":"Keystone Park","county":"","formationDate":"2004-03-14","managementStartDate":"2026-11-01"}""", ["county"] },
        { """{"legalName":"Keystone Park HOA, Inc.","communityName":"Keystone Park","county":"Hamilton","managementStartDate":"2026-11-01"}""", ["formationDate"] },
        { """{"legalName":"Keystone Park HOA, Inc.","communityName":"Keystone Park","county":"Hamilton","formationDate":"2004-03-14"}""", ["managementStartDate"] },
        { """{"communityName":"Keystone Park"}""", ["county", "formationDate", "legalName", "managementStartDate"] },
        { $$"""{"legalName":"{{new string('L', 201)}}","communityName":"Keystone Park","county":"Hamilton","formationDate":"2004-03-14","managementStartDate":"2026-11-01"}""", ["legalName"] },
        { """{"legalName":"Keystone Park HOA, Inc.","communityName":"K","county":"Hamilton","formationDate":"2004-03-14","managementStartDate":"2026-11-01"}""", ["communityName"] },
        { $$"""{"legalName":"Keystone Park HOA, Inc.","communityName":"{{new string('K', 81)}}","county":"Hamilton","formationDate":"2004-03-14","managementStartDate":"2026-11-01"}""", ["communityName"] },
        { $$"""{"legalName":"Keystone Park HOA, Inc.","communityName":"Keystone Park","county":"Hamilton","formationDate":"2004-03-14","managementStartDate":"2026-11-01","description":"{{new string('D', 2001)}}"}""", ["description"] },
        { $$"""{"legalName":"Keystone Park HOA, Inc.","communityName":"Keystone Park","county":"Hamilton","formationDate":"{{DateTime.UtcNow.AddDays(1):yyyy-MM-dd}}","managementStartDate":"2026-11-01"}""", ["formationDate"] },
    };

    // US1-S9
    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidFields_Are422_NamingExactlyTheFieldsAtFault(string json, string[] fields)
    {
        var companyId = await CreateCompanyAsync();
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);
        var before = await CountAsync(companyId);

        var res = await client.PostAsync("/api/v1/communities", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        Assert.Equal(fields, body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()!).Distinct().Order());
        Assert.Equal(before, await CountAsync(companyId));
    }

    [Fact]
    public async Task AnArchivedParent_IsRefusedWithParentArchived()
    {
        var companyId = await CreateCompanyAsync();
        var parentName = "Old Mill Commons " + Guid.NewGuid().ToString("N")[..6];
        var parent = await CreateCommunityInAsync(companyId, CommunityStatus.Archived, name: parentName);
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var res = await client.PostAsJsonAsync("/api/v1/communities", Body("Mill Row " + Guid.NewGuid().ToString("N")[..6], parent: parent));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("PARENT_ARCHIVED", body.GetProperty("code").GetString());
        Assert.Contains(parentName, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AParentInAnotherCompany_IsForbidden()
    {
        var companyId = await CreateCompanyAsync();
        var foreign = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        await AssertForbiddenAsync(await client.PostAsJsonAsync("/api/v1/communities",
            Body("Keystone Park " + Guid.NewGuid().ToString("N")[..8], parent: foreign)));
    }

    private static object Body(string communityName, Guid? parent = null) => new
    {
        legalName = "Keystone Park HOA, Inc.",
        communityName,
        county = "Hamilton",
        formationDate = "2004-03-14",
        managementStartDate = "2026-11-01",
        description = (string?)null,
        parentCommunityId = parent,
    };

    private Task<int> CountAsync(Guid companyId) =>
        WithDbAsync(db => db.Communities.CountAsync(c => c.ManagementCompanyId == companyId));

    private static async Task AssertForbiddenAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("FORBIDDEN", "You do not have access to this community."),
            (body.GetProperty("code").GetString(), body.GetProperty("message").GetString()));
    }
}

using System.Net;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T065 — <c>GET /communities/{id}/settings</c>, the Community settings page shell
/// (contracts/community-settings.md; US5, FR-010, FR-029).
/// </summary>
public class CommunitySettingsShellTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private static readonly string[] Sections = ["profile", "managers-and-memberships", "architectural-review"];

    // US5-S1, per status: every section, and the status actions that fit the status.
    [Theory]
    [InlineData(CommunityStatus.Onboarding, true, true, false, false)]
    [InlineData(CommunityStatus.Active, false, true, false, false)]
    [InlineData(CommunityStatus.Archived, false, false, true, true)]
    public async Task CompanyAdministrator_WithNoMembership_GetsEverySectionAndTheStatusActions(
        CommunityStatus status, bool activate, bool archive, bool restore, bool archivedActions)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, status, name: $"Keystone Park {Guid.NewGuid():N}");
        await CreateManagerAsync(community);
        var (_, email) = await CreateCompanyAdminAsync(companyId);

        var body = await GetSettingsAsync(email, community);

        Assert.Equal(community, body.GetProperty("id").GetGuid());
        Assert.Equal(status.ToString(), body.GetProperty("status").GetString());
        Assert.Equal(Sections, body.GetProperty("sections").EnumerateArray().Select(s => s.GetString()));
        Assert.False(body.GetProperty("noManager").GetBoolean());
        var can = body.GetProperty("can");
        var writable = status != CommunityStatus.Archived;
        Assert.Equal(new Dictionary<string, bool>
        {
            ["editProfile"] = writable, ["changeParent"] = writable, ["manageManagers"] = writable,
            ["replaceLastManager"] = writable, ["manageMemberships"] = writable, ["manageResidents"] = writable,
            ["editArcSettings"] = writable, ["endMembershipsWhileArchived"] = archivedActions,
        }, Flags(can));
        Assert.Equal((activate, archive, restore),
            (Allowed(can, "activate"), Allowed(can, "archive"), Allowed(can, "restore")));
        // An Onboarding community with an open-ended manager can be marked Active.
        if (status == CommunityStatus.Onboarding)
            Assert.Equal(JsonValueKind.Null, can.GetProperty("activate").GetProperty("reasonCode").ValueKind);
    }

    // FR-010: no open-ended manager → Mark Active is disabled with NO_ACTIVE_MANAGER.
    [Fact]
    public async Task WithNoOpenEndedManager_ActivateGivesNoActiveManager_AndNoManagerIsShown()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        // A manager with an end date counts for neither activation nor the "No manager" flag.
        await CreateManagerAsync(community, endDate: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3));
        var bare = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, email) = await CreateCompanyAdminAsync(companyId);

        var withTermManager = await GetSettingsAsync(email, community);
        var withNoManager = await GetSettingsAsync(email, bare);

        foreach (var body in new[] { withTermManager, withNoManager })
        {
            var activate = body.GetProperty("can").GetProperty("activate");
            Assert.False(activate.GetProperty("allowed").GetBoolean());
            Assert.Equal("NO_ACTIVE_MANAGER", activate.GetProperty("reasonCode").GetString());
        }
        // Brief 04 D6: "No manager" means no open-ended Community Manager, so a term manager doesn't count.
        Assert.True(withTermManager.GetProperty("noManager").GetBoolean());
        Assert.True(withNoManager.GetProperty("noManager").GetBoolean());
    }

    // US5-S2: the community's manager edits everything but status and parent.
    [Fact]
    public async Task CommunityManager_GetsNoStatusOrParentActions()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, email) = await CreateManagerAsync(community);

        var body = await GetSettingsAsync(email, community);

        Assert.Equal(Sections, body.GetProperty("sections").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("noManager").ValueKind);
        var can = body.GetProperty("can");
        Assert.Equal(new Dictionary<string, bool>
        {
            ["editProfile"] = true, ["changeParent"] = false, ["manageManagers"] = true,
            ["replaceLastManager"] = false, ["manageMemberships"] = true, ["manageResidents"] = true,
            ["editArcSettings"] = true, ["endMembershipsWhileArchived"] = false,
        }, Flags(can));
        Assert.Equal((false, false, false), (Allowed(can, "activate"), Allowed(can, "archive"), Allowed(can, "restore")));
    }

    // US5-S3: anyone else is refused exactly as for a community that doesn't exist.
    [Fact]
    public async Task BoardMemberAccountantResidentAndOtherManagers_AreRefusedLikeANonexistentCommunity()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var other = await CreateCommunityInAsync(companyId);
        var (_, board) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (_, accountant) = await CreateStaffAsync(community, CommunityRole.Accountant);
        var (_, resident) = await CreateResidentAsync(await CreatePropertyAsync(community));
        var (_, otherManager) = await CreateManagerAsync(other);
        var (_, otherCompanyAdmin) = await CreateCompanyAdminAsync(await CreateCompanyAsync());

        using var managerClient = await ClientForAsync(otherManager);
        var missing = await managerClient.GetAsync($"/api/v1/communities/{Guid.NewGuid()}/settings");
        var expected = await missing.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);

        foreach (var email in new[] { board, accountant, resident, otherManager, otherCompanyAdmin })
        {
            using var client = await ClientForAsync(email);
            var res = await client.GetAsync($"/api/v1/communities/{community}/settings");
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal(expected, await res.Content.ReadAsStringAsync());
            Assert.Equal("FORBIDDEN", JsonDocument.Parse(expected).RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task TheShell_ReportsTheParentAndMissingRequiredFields_AndIsNeverCached()
    {
        var companyId = await CreateCompanyAsync();
        var parent = await CreateCommunityInAsync(companyId, name: $"Keystone Crossing Master {Guid.NewGuid():N}");
        var community = await CreateCommunityInAsync(companyId, parentId: parent);
        await WithDbAsync(db => db.Communities.Where(c => c.Id == community)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.County, (string?)null).SetProperty(c => c.FormationDate, (DateOnly?)null)));
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var res = await client.GetAsync($"/api/v1/communities/{community}/settings");

        Assert.True(res.Headers.CacheControl?.NoStore);
        var body = await JsonAsync(res);
        Assert.Equal(parent, body.GetProperty("parent").GetProperty("id").GetGuid());
        Assert.StartsWith("Keystone Crossing Master", body.GetProperty("parent").GetProperty("communityName").GetString());
        Assert.Equal(["county", "formationDate", "managementStartDate"],
            body.GetProperty("missingRequiredFields").EnumerateArray().Select(f => f.GetString()));
        Assert.DoesNotContain("notifications", body.GetProperty("sections").EnumerateArray().Select(s => s.GetString()));
    }

    // A sub-association can't be restored while its parent is archived (FR-010).
    [Fact]
    public async Task RestoreUnderAnArchivedParent_GivesParentArchived()
    {
        var companyId = await CreateCompanyAsync();
        var parent = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Archived, parentId: parent);
        var (_, email) = await CreateCompanyAdminAsync(companyId);

        var restore = (await GetSettingsAsync(email, community)).GetProperty("can").GetProperty("restore");

        Assert.False(restore.GetProperty("allowed").GetBoolean());
        Assert.Equal("PARENT_ARCHIVED", restore.GetProperty("reasonCode").GetString());
    }

    private async Task<JsonElement> GetSettingsAsync(string email, Guid communityId)
    {
        using var client = await ClientForAsync(email);
        var res = await client.GetAsync($"/api/v1/communities/{communityId}/settings");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await JsonAsync(res);
    }

    private static Dictionary<string, bool> Flags(JsonElement can) => can.EnumerateObject()
        .Where(p => p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        .ToDictionary(p => p.Name, p => p.Value.GetBoolean());

    private static bool Allowed(JsonElement can, string action) => can.GetProperty(action).GetProperty("allowed").GetBoolean();
}

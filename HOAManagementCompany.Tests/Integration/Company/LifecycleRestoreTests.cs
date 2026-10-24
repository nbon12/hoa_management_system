using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T096 — restoring an Archived community (US3; FR-010, FR-013; contracts/company.md § restore).
/// </summary>
public class LifecycleRestoreTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US3-S12
    [Fact]
    public async Task Restoring_ACommunityThatWasActive_WithAnOpenEndedManager_ReturnsItToActive_AsItWas()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        await CreateStaffAsync(community, CommunityRole.BoardMember, endDate: Today.AddMonths(8));
        await CreateStaffAsync(community, CommunityRole.Accountant);
        var home = await CreatePropertyAsync(community);
        await CreateResidentAsync(home);
        var autopay = await CreateAutopayAsync(home, null);
        var (managerId, _) = await CreateManagerAsync(community);
        var (invitation, _) = await InsertResidencyInvitationAsync(companyId, community, await CreatePropertyAsync(community),
            $"pat-{Guid.NewGuid():N}@example.com", managerId, InvitationState.Pending);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var membershipsBefore = await MembershipsAsync(community);
        var arcBefore = await ArcSettingsAsync(community);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/restore", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("Active", "WasActiveWithManager"), (body.GetProperty("status").GetString(), body.GetProperty("reason").GetString()));
        var row = await WithDbAsync(db => db.Communities.AsNoTracking().SingleAsync(c => c.Id == community));
        Assert.Equal((CommunityStatus.Active, (DateTimeOffset?)Clock.UtcNow), (row.Status, row.RestoredAt));
        Assert.Equal(membershipsBefore, await MembershipsAsync(community));
        Assert.Equal(arcBefore, await ArcSettingsAsync(community));
        // Nothing the archive ended comes back.
        Assert.Equal("inactive", await WithDbAsync(db => db.RecurringPayments.Where(r => r.Id == autopay).Select(r => r.Status).SingleAsync()));
        Assert.Equal(InvitationState.Revoked, await WithDbAsync(db => db.Invitations.Where(i => i.Id == invitation).Select(i => i.State).SingleAsync()));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityLifecycle")
            && e.Properties["Action"].ToString() == "\"Restored\"" && e.Properties["To"].ToString() == "\"Active\"");
    }

    // US3-S13
    [Fact]
    public async Task Restoring_ACommunityArchivedWhileOnboarding_ReturnsItToOnboarding()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var (residentId, residentEmail) = await CreateResidentAsync(home);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);

        var body = await JsonAsync(await admin.PostAsync($"/api/v1/communities/{community}/restore", null));

        Assert.Equal(("Onboarding", "NeverActivated"), (body.GetProperty("status").GetString(), body.GetProperty("reason").GetString()));
        // Hidden from residents again: left out of the home list, and refused when asked for.
        using (var signedIn = await ClientForAsync(residentEmail))
        {
            var me = await JsonAsync(await signedIn.GetAsync("/api/v1/auth/me"));
            Assert.Empty(me.GetProperty("properties").EnumerateArray());
            Assert.Equal("NotLive", me.GetProperty("landing").GetString());
        }
        using var resident = ClientWithToken(MintToken(residentId, home, community));
        var ledger = await resident.GetAsync("/api/v1/payments/ledger");
        Assert.Equal(HttpStatusCode.Conflict, ledger.StatusCode);
        Assert.Equal("COMMUNITY_NOT_ACTIVE", (await JsonAsync(ledger)).GetProperty("code").GetString());
    }

    // US3-S14
    [Fact]
    public async Task Restoring_AfterTheOnlyManagersEndDatePassed_ReturnsToOnboarding_AndActivatingIsRefused()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community, endDate: Today.AddDays(3));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using (var before = await ClientForAsync(adminEmail))
            Assert.Equal(HttpStatusCode.OK, (await before.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);
        Clock.UtcNow = Clock.UtcNow.AddDays(10);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/restore", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("Onboarding", "NoOpenEndedManager"), (body.GetProperty("status").GetString(), body.GetProperty("reason").GetString()));
        var portfolio = await JsonAsync(await admin.GetAsync("/api/v1/company/communities"));
        var item = portfolio.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == community);
        Assert.True(item.GetProperty("noManager").GetBoolean());

        var activate = await admin.PostAsync($"/api/v1/communities/{community}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
        Assert.Equal("NO_ACTIVE_MANAGER", (await JsonAsync(activate)).GetProperty("code").GetString());
    }

    // US3-S15
    [Fact]
    public async Task ACompanyAdministrator_EndsTheManagerWhileArchived_AndAfterRestoreTheyHaveNoAccess()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var membership = await WithDbAsync(db => db.CommunityMemberships
            .Where(m => m.CommunityId == community && m.UserId == managerId).Select(m => m.Id).SingleAsync());
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        using var manager = await ClientForAsync(managerEmail);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);

        var end = await admin.PatchAsJsonAsync($"/api/v1/communities/{community}/memberships/{membership}", new { end = true });

        Assert.Equal(HttpStatusCode.OK, end.StatusCode);
        Assert.Equal(MembershipStatus.Inactive,
            await WithDbAsync(db => db.CommunityMemberships.Where(m => m.Id == membership).Select(m => m.Status).SingleAsync()));

        var restore = await JsonAsync(await admin.PostAsync($"/api/v1/communities/{community}/restore", null));
        Assert.Equal("Onboarding", restore.GetProperty("status").GetString());

        // M's token still names the community; the request is checked against today's memberships.
        var next = await manager.GetAsync($"/api/v1/communities/{community}/memberships");
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(next)).GetProperty("code").GetString());
    }

    // US3-S16
    [Fact]
    public async Task ASubAssociation_CantBeRestoredWhileItsParentIsArchived_AndRestoringTheParentLeavesItArchived()
    {
        var companyId = await CreateCompanyAsync();
        var parentName = $"Keystone Park {Guid.NewGuid():N}"[..24];
        var parent = await CreateCommunityInAsync(companyId, CommunityStatus.Archived, parentName);
        await CreateManagerAsync(parent);
        var sub = await CreateCommunityInAsync(companyId, CommunityStatus.Archived, parentId: parent);
        await CreateManagerAsync(sub);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var refused = await admin.PostAsync($"/api/v1/communities/{sub}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = await JsonAsync(refused);
        Assert.Equal("PARENT_ARCHIVED", error.GetProperty("code").GetString());
        Assert.Contains(parentName, error.GetProperty("message").GetString());
        Assert.Equal(CommunityStatus.Archived, await StatusAsync(sub));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{parent}/restore", null)).StatusCode);
        Assert.Equal(CommunityStatus.Active, await StatusAsync(parent));
        Assert.Equal(CommunityStatus.Archived, await StatusAsync(sub));
    }

    // Spec Edge Cases: an archived community keeps its name reserved, so a restore never collides.
    [Fact]
    public async Task AnArchivedCommunitysName_StaysReserved()
    {
        var companyId = await CreateCompanyAsync();
        var name = $"Keystone Ridge {Guid.NewGuid():N}"[..26];
        var community = await CreateCommunityInAsync(companyId, name: name);
        await CreateManagerAsync(community);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);

        var taken = await admin.PostAsJsonAsync("/api/v1/communities", new
        {
            legalName = "Keystone Ridge HOA, Inc.", communityName = " " + name.ToUpperInvariant(), county = "Wake",
            formationDate = "2004-03-14", managementStartDate = "2026-11-01",
        });

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("COMMUNITY_NAME_TAKEN", (await JsonAsync(taken)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/restore", null)).StatusCode);
        Assert.Equal(name, await WithDbAsync(db => db.Communities.Where(c => c.Id == community).Select(c => c.CommunityName).SingleAsync()));
    }

    [Fact]
    public async Task TheRestorePreview_ListsTheMembershipsRegainingAccess()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var (managerId, _) = await CreateManagerAsync(community);
        var (boardId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember, endDate: Today.AddMonths(2));
        await CreateStaffAsync(community, CommunityRole.Accountant, status: MembershipStatus.Inactive);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var preview = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/lifecycle/restore/preview"));

        Assert.True(preview.GetProperty("allowed").GetBoolean());
        var consequences = preview.GetProperty("consequences");
        Assert.Equal("Active", consequences.GetProperty("returnsTo").GetString());
        var regaining = consequences.GetProperty("membershipsRegainingAccess").EnumerateArray()
            .Select(m => (m.GetProperty("userId").GetString(), m.GetProperty("role").GetString()))
            .OrderBy(m => m.Item2).ToList();
        Assert.Equal([(boardId, "BoardMember"), (managerId, "CommunityManager")], regaining);
    }

    // US3-S20
    [Fact]
    public async Task ACommunityManager_CantRestore_OrPreviewARestore()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var restore = await manager.PostAsync($"/api/v1/communities/{community}/restore", null);
        var preview = await manager.GetAsync($"/api/v1/communities/{community}/lifecycle/restore/preview");

        foreach (var res in new[] { restore, preview })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.Equal(CommunityStatus.Archived, await StatusAsync(community));
    }

    private DateOnly Today => DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime);

    private Task<CommunityStatus> StatusAsync(Guid community) =>
        WithDbAsync(db => db.Communities.Where(c => c.Id == community).Select(c => c.Status).SingleAsync());

    private Task<string> MembershipsAsync(Guid community) => WithDbAsync(async db =>
        string.Join(",", await db.CommunityMemberships.AsNoTracking().Where(m => m.CommunityId == community)
            .OrderBy(m => m.Id).Select(m => $"{m.Id}:{m.UserId}:{m.Role}:{m.Status}:{m.StartDate}:{m.EndDate}").ToListAsync()));

    private Task<string> ArcSettingsAsync(Guid community) => WithDbAsync(async db =>
        JsonSerializer.Serialize(await db.CommunityArcSettings.AsNoTracking().SingleAsync(s => s.CommunityId == community)));
}

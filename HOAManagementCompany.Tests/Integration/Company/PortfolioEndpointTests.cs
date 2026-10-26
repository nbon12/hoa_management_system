using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T072 — the first version of <c>GET /company/communities</c> (contracts/company.md), which
/// US6 extends: the caller's company only, A–Z, paged, and Company Administrators only.
/// </summary>
public class PortfolioEndpointTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task CompanyAdministrator_SeesTheirCompanysCommunities_AToZ_WithNameLegalNameStatusAndParent()
    {
        var companyId = await CreateCompanyAsync();
        var master = await CreateCommunityInAsync(companyId, name: "Keystone Crossing Master", legalName: "KCM HOA, Inc.");
        await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding, name: "keystone park");
        await CreateCommunityInAsync(companyId, CommunityStatus.Archived, name: "Briar Hollow");
        var sub = await CreateCommunityInAsync(companyId, name: "Keystone Crossing SF", parentId: master);
        await CreateCommunityInAsync(await CreateCompanyAsync(), name: "Another Company's Community");
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var res = await client.GetAsync("/api/v1/company/communities");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);
        var body = await JsonAsync(res);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Briar Hollow", "Keystone Crossing Master", "Keystone Crossing SF", "keystone park"],
            items.Select(i => i.GetProperty("communityName").GetString()));
        Assert.Equal(4, body.GetProperty("total").GetInt32());
        Assert.Equal(("Archived", "Onboarding"),
            (items[0].GetProperty("status").GetString(), items[3].GetProperty("status").GetString()));
        Assert.Equal("KCM HOA, Inc.", items[1].GetProperty("legalName").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("parent").ValueKind);
        var parent = items[2].GetProperty("parent");
        Assert.Equal((master, "Keystone Crossing Master"), (parent.GetProperty("id").GetGuid(), parent.GetProperty("communityName").GetString()));
        Assert.Equal(sub, items[2].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ThePageSizeDefaultsTo25_AndIsClampedAt100()
    {
        var companyId = await CreateCompanyAsync();
        await WithDbAsync(async db =>
        {
            for (var i = 0; i < 26; i++)
            {
                var id = Guid.NewGuid();
                db.Communities.Add(new Domain.Entities.Community
                {
                    Id = id, ManagementCompanyId = companyId, CommunityName = $"Community {i:D3}",
                    LegalName = $"Community {i:D3}, Inc.", Status = CommunityStatus.Active,
                });
                db.CommunityArcSettings.Add(Domain.Entities.CommunityArcSettings.Defaults(id));
            }
            await db.SaveChangesAsync();
        });
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var first = await JsonAsync(await client.GetAsync("/api/v1/company/communities"));
        var clamped = await JsonAsync(await client.GetAsync("/api/v1/company/communities?limit=101"));
        var second = await JsonAsync(await client.GetAsync("/api/v1/company/communities?offset=25"));

        Assert.Equal((25, 25, 26), (first.GetProperty("items").GetArrayLength(), first.GetProperty("limit").GetInt32(), first.GetProperty("total").GetInt32()));
        Assert.Equal((26, 100), (clamped.GetProperty("items").GetArrayLength(), clamped.GetProperty("limit").GetInt32()));
        Assert.Equal("Community 025", Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("communityName").GetString());
    }

    // US6-S6, backend half: the portfolio is Company Administrators only.
    [Fact]
    public async Task CommunityManager_IsForbidden()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, email) = await CreateManagerAsync(community);
        using var client = await ClientForAsync(email);

        var res = await client.GetAsync("/api/v1/company/communities");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        // Their own communities stay in their switcher.
        var mine = await client.GetAsync("/api/v1/me/communities");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Contains((await JsonAsync(mine)).GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == community);
    }

    // US6-S1
    [Fact]
    public async Task ACommunityWithNoOpenEndedManager_IsFlaggedNoManager()
    {
        var companyId = await CreateCompanyAsync();
        var staffed = await CreateCommunityInAsync(companyId, name: "Alder Ridge");
        var (managerId, _) = await CreateManagerAsync(staffed);
        var endDated = await CreateCommunityInAsync(companyId, name: "Birch Hollow");
        await CreateManagerAsync(endDated, endDate: DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddMonths(3));
        await CreateCommunityInAsync(companyId, name: "Cedar Glen");

        var items = await ItemsAsync(companyId, "");

        Assert.Equal([("Alder Ridge", false), ("Birch Hollow", true), ("Cedar Glen", true)],
            items.Select(i => (i.GetProperty("communityName").GetString(), i.GetProperty("noManager").GetBoolean())));
        var manager = Assert.Single(items[0].GetProperty("managers").EnumerateArray());
        Assert.Equal((managerId, "Board Tester"), (manager.GetProperty("userId").GetString(), manager.GetProperty("name").GetString()));
        Assert.Empty(items[1].GetProperty("managers").EnumerateArray());
    }

    // US6-S2
    [Fact]
    public async Task OnlyAPendingManagerInvitation_IsStillNoManager_AndIsListed()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, name: "Keystone Park");
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var pat = $"pat-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.CommunityManager, email = pat })).StatusCode);

        var item = Assert.Single(await ItemsAsync(companyId, "", admin));

        Assert.True(item.GetProperty("noManager").GetBoolean());
        var pending = Assert.Single(item.GetProperty("pendingManagerInvitations").EnumerateArray());
        Assert.Equal((pat, Clock.UtcNow.AddHours(72)), (pending.GetProperty("email").GetString(), pending.GetProperty("expiresAt").GetDateTimeOffset()));
    }

    // US6-S3
    [Fact]
    public async Task ARuleThatDiffersFromTheDefaults_IsFlagged()
    {
        var companyId = await CreateCompanyAsync();
        var differs = await CreateCommunityInAsync(companyId, name: "Alder Ridge");
        await CreateCommunityInAsync(companyId, name: "Birch Hollow");
        await WithDbAsync(db => db.CommunityArcSettings.Where(s => s.CommunityId == differs)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReviewPeriodDays, 45)));

        var items = await ItemsAsync(companyId, "");

        Assert.Equal([true, false], items.Select(i => i.GetProperty("differsFromDefaults").GetBoolean()));
    }

    // US6-S4, US6-S5: "contains" matches mid-name as well as at the start, in both the community name
    // and the legal name, and a sub isn't listed just because its parent's name matches.
    [Fact]
    public async Task TheStatusFilterAndSearch_NarrowTheList()
    {
        var companyId = await CreateCompanyAsync();
        await CreateCommunityInAsync(companyId, CommunityStatus.Archived, name: "Briar Hollow");
        await CreateCommunityInAsync(companyId, CommunityStatus.Archived, name: "Alder Ridge", legalName: "KEYSTONE Holdings, Inc.");
        await CreateCommunityInAsync(companyId, name: "keystone park");
        await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding, name: "Cedar Glen");
        await CreateCommunityInAsync(companyId, name: "Old Mill at keystone");
        await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding, name: "Dogwood Run", legalName: "The Keystone Group LLC");
        var master = await CreateCommunityInAsync(companyId, name: "Keystone Crossing Master");
        await CreateCommunityInAsync(companyId, name: "Elm Court", parentId: master, legalName: "Elm Court HOA");

        Assert.Equal(["Alder Ridge", "Briar Hollow"], Names(await ItemsAsync(companyId, "?status=archived")));
        Assert.Equal(["Cedar Glen", "Dogwood Run"], Names(await ItemsAsync(companyId, "?status=Onboarding")));
        Assert.Equal(["Elm Court", "Keystone Crossing Master", "keystone park", "Old Mill at keystone"],
            Names(await ItemsAsync(companyId, "?status=active")));
        Assert.Equal(["Alder Ridge", "Dogwood Run", "Keystone Crossing Master", "keystone park", "Old Mill at keystone"],
            Names(await ItemsAsync(companyId, "?search=Keystone")));
        Assert.Equal(["Alder Ridge"], Names(await ItemsAsync(companyId, "?status=archived&search=keyStone")));
    }

    // US6-S7: the "No manager" filter and count use FR-020's open-ended manager, not "no membership".
    [Fact]
    public async Task With31Communities_TheListSortsFiltersAndCounts()
    {
        var companyId = await CreateCompanyAsync();
        var today = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime);
        await WithDbAsync(async db =>
        {
            for (var i = 1; i <= 31; i++)
            {
                var id = Guid.NewGuid();
                var status = i <= 3 ? CommunityStatus.Onboarding : i <= 5 ? CommunityStatus.Archived : CommunityStatus.Active;
                db.Communities.Add(new Domain.Entities.Community
                {
                    Id = id, ManagementCompanyId = companyId, CommunityName = $"Community {i}", LegalName = $"Community {i}, Inc.",
                    Status = status,
                });
                db.CommunityArcSettings.Add(Domain.Entities.CommunityArcSettings.Defaults(id));
                // Every community but #7–#12 has an open-ended manager. #7 and #8 have no one. #9–#11 each
                // have only a membership that isn't an open-ended manager: a manager with an end date, an
                // open-ended Board Member, an inactive manager. #12 has only a pending manager invitation.
                if (i == 12)
                    db.Invitations.Add(new Domain.Entities.Invitation
                    {
                        Id = Guid.NewGuid(), Kind = InvitationKind.Membership, CompanyId = companyId, CommunityId = id,
                        Role = CommunityRole.CommunityManager, Email = $"pat-{id:N}@example.com",
                        NormalizedEmail = $"pat-{id:N}@example.com", State = InvitationState.Pending,
                        SenderUserId = await CreateUserAsync(db), CreatedAt = Clock.UtcNow,
                        TokenHash = Features.Invitations.InvitationTokens.Hash(Features.Invitations.InvitationTokens.New()),
                        SentAt = Clock.UtcNow, ExpiresAt = Clock.UtcNow.AddHours(72),
                    });
                else if (i is not (7 or 8))
                    db.CommunityMemberships.Add(new Domain.Entities.CommunityMembership
                    {
                        Id = Guid.NewGuid(), CommunityId = id, UserId = await CreateUserAsync(db),
                        Role = i == 10 ? CommunityRole.BoardMember : CommunityRole.CommunityManager,
                        Status = i == 11 ? MembershipStatus.Inactive : MembershipStatus.Active, StartDate = new DateOnly(2025, 1, 1),
                        EndDate = i == 9 ? today.AddMonths(3) : null,
                    });
            }
            await db.SaveChangesAsync();
        });
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var first = await JsonAsync(await client.GetAsync("/api/v1/company/communities?limit=100"));
        Assert.Equal(Enumerable.Range(1, 31).Select(i => $"Community {i}"), Names(first.GetProperty("items").EnumerateArray().ToList()));
        var counts = first.GetProperty("counts");
        Assert.Equal((31, 3, 26, 2, 6), (counts.GetProperty("all").GetInt32(), counts.GetProperty("onboarding").GetInt32(),
            counts.GetProperty("active").GetInt32(), counts.GetProperty("archived").GetInt32(), counts.GetProperty("noManager").GetInt32()));

        var byStatus = Names((await JsonAsync(await client.GetAsync("/api/v1/company/communities?sort=status&limit=6")))
            .GetProperty("items").EnumerateArray().ToList());
        Assert.Equal(["Community 1", "Community 2", "Community 3", "Community 6", "Community 7", "Community 8"], byStatus);
        var desc = Names((await JsonAsync(await client.GetAsync("/api/v1/company/communities?sort=name&dir=desc&limit=2")))
            .GetProperty("items").EnumerateArray().ToList());
        Assert.Equal(["Community 31", "Community 30"], desc);

        var noManager = await JsonAsync(await client.GetAsync("/api/v1/company/communities?noManager=true&search=community"));
        Assert.Equal(["Community 7", "Community 8", "Community 9", "Community 10", "Community 11", "Community 12"],
            Names(noManager.GetProperty("items").EnumerateArray().ToList()));
        Assert.Equal(6, noManager.GetProperty("total").GetInt32());
        // The counts ignore the search, the filters and paging.
        Assert.Equal(31, noManager.GetProperty("counts").GetProperty("all").GetInt32());
    }

    // contracts/company.md § GET /company/communities, sort=parent: communities without a parent
    // first, then each parent's subs together; names break ties, in the same direction.
    [Fact]
    public async Task SortingByParent_GroupsSubsUnderTheirParent_InBothDirections()
    {
        var companyId = await CreateCompanyAsync();
        var alder = await CreateCommunityInAsync(companyId, name: "Alder Master");
        var willow = await CreateCommunityInAsync(companyId, name: "Willow Master");
        await CreateCommunityInAsync(companyId, name: "Cedar Glen");
        await CreateCommunityInAsync(companyId, name: "Zeta Sub", parentId: alder);
        await CreateCommunityInAsync(companyId, name: "Beta Sub", parentId: willow);
        await CreateCommunityInAsync(companyId, name: "Gamma Sub", parentId: alder);
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var asc = await ItemsAsync(companyId, "?sort=parent", client);
        var desc = await ItemsAsync(companyId, "?sort=Parent&dir=DESC", client);

        Assert.Equal(["Alder Master", "Cedar Glen", "Willow Master", "Gamma Sub", "Zeta Sub", "Beta Sub"], Names(asc));
        Assert.Equal(["Beta Sub", "Zeta Sub", "Gamma Sub", "Willow Master", "Cedar Glen", "Alder Master"], Names(desc));
        Assert.Equal("Alder Master", asc[3].GetProperty("parent").GetProperty("communityName").GetString());
    }

    // contracts/company.md § Portfolio: unknown filter values and an over-long search are refused.
    [Theory]
    [InlineData("?status=closed", "status", "status must be onboarding, active or archived.")]
    [InlineData("?sort=created", "sort", "sort must be name, status or parent.")]
    [InlineData("?dir=up", "dir", "dir must be asc or desc.")]
    [InlineData("?search=LONG", "search", "Search can be at most 100 characters.")]
    public async Task AnUnknownFilterOrSort_OrATooLongSearch_Is422_OnThatField(string query, string field, string message)
    {
        var companyId = await CreateCompanyAsync();
        await CreateCommunityInAsync(companyId);
        var (_, email) = await CreateCompanyAdminAsync(companyId);
        using var client = await ClientForAsync(email);

        var res = await client.GetAsync($"/api/v1/company/communities{query.Replace("LONG", new string('k', 101))}");

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal((field, message), (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
    }

    private async Task<List<JsonElement>> ItemsAsync(Guid companyId, string query, HttpClient? client = null)
    {
        HttpClient? own = null;
        if (client is null)
        {
            var (_, email) = await CreateCompanyAdminAsync(companyId);
            client = own = await ClientForAsync(email);
        }
        try
        {
            var res = await client.GetAsync($"/api/v1/company/communities{query}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return (await JsonAsync(res)).GetProperty("items").EnumerateArray().ToList();
        }
        finally
        {
            own?.Dispose();
        }
    }

    private static List<string> Names(List<JsonElement> items) =>
        items.Select(i => i.GetProperty("communityName").GetString()!).ToList();
}

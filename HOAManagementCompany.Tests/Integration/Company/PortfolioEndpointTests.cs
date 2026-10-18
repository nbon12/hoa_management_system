using System.Net;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
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
        var (_, email) = await CreateManagerAsync(await CreateCommunityInAsync(companyId));
        using var client = await ClientForAsync(email);

        var res = await client.GetAsync("/api/v1/company/communities");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
    }
}

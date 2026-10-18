using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>030 T097 — PUT /communities/{id}/parent (FR-016; contracts/company.md § parent).</summary>
public class CommunityParentTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task ACompanyAdministrator_SetsAndClearsTheParent_AndEachChangeIsLogged()
    {
        var companyId = await CreateCompanyAsync();
        var parent = await CreateCommunityInAsync(companyId);
        var community = await CreateCommunityInAsync(companyId);
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var set = await admin.PutAsJsonAsync(Url(community), new { parentCommunityId = parent });

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(parent, (await JsonAsync(set)).GetProperty("parentCommunityId").GetGuid());
        Assert.Equal(parent, await ParentAsync(community));
        AssertLogged(adminId, community, from: "", to: parent.ToString());

        var clear = await admin.PutAsJsonAsync(Url(community), new { parentCommunityId = (Guid?)null });

        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Null(await ParentAsync(community));
        AssertLogged(adminId, community, from: parent.ToString(), to: "");
    }

    [Fact]
    public async Task ChoosingTheCommunitysOwnDescendant_IsACycle()
    {
        var companyId = await CreateCompanyAsync();
        var top = await CreateCommunityInAsync(companyId, name: $"Keystone Park {Guid.NewGuid():N}"[..24]);
        var middle = await CreateCommunityInAsync(companyId, parentId: top);
        var bottomName = $"Keystone Glen {Guid.NewGuid():N}"[..24];
        var bottom = await CreateCommunityInAsync(companyId, name: bottomName, parentId: middle);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PutAsJsonAsync(Url(top), new { parentCommunityId = bottom });
        var itself = await admin.PutAsJsonAsync(Url(top), new { parentCommunityId = top });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("PARENT_CYCLE", body.GetProperty("code").GetString());
        Assert.Contains(bottomName, body.GetProperty("message").GetString());
        Assert.Equal((HttpStatusCode)422, itself.StatusCode);
        Assert.Equal("PARENT_CYCLE", (await JsonAsync(itself)).GetProperty("code").GetString());
        Assert.Null(await ParentAsync(top));
    }

    [Fact]
    public async Task AnArchivedParent_IsRefused()
    {
        var companyId = await CreateCompanyAsync();
        var parentName = $"Keystone Park {Guid.NewGuid():N}"[..24];
        var parent = await CreateCommunityInAsync(companyId, CommunityStatus.Archived, parentName);
        var community = await CreateCommunityInAsync(companyId);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PutAsJsonAsync(Url(community), new { parentCommunityId = parent });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("PARENT_ARCHIVED", body.GetProperty("code").GetString());
        Assert.Contains(parentName, body.GetProperty("message").GetString());
        Assert.Null(await ParentAsync(community));
    }

    // US3-S20
    [Fact]
    public async Task ACommunityManager_CantChangeTheParent()
    {
        var companyId = await CreateCompanyAsync();
        var parent = await CreateCommunityInAsync(companyId);
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        await WithDbAsync(async db =>
        {
            db.CommunityMemberships.Add(new Domain.Entities.CommunityMembership
            {
                Id = Guid.NewGuid(), CommunityId = parent, UserId = managerId, Role = CommunityRole.CommunityManager,
                Status = MembershipStatus.Active, StartDate = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(-30),
            });
            await db.SaveChangesAsync();
        });
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PutAsJsonAsync(Url(community), new { parentCommunityId = parent });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await ParentAsync(community));
    }

    private static string Url(Guid community) => $"/api/v1/communities/{community}/parent";

    private Task<Guid?> ParentAsync(Guid community) =>
        WithDbAsync(db => db.Communities.Where(c => c.Id == community).Select(c => c.ParentCommunityId).SingleAsync());

    private void AssertLogged(string actorId, Guid community, string from, string to) =>
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityLifecycle")
            && e.Properties["Action"].ToString() == "\"ParentChanged\""
            && e.Properties["ActorId"].ToString() == $"\"{actorId}\""
            && e.Properties["CommunityId"].ToString() == community.ToString()
            && Unquote(e.Properties["From"].ToString()) == from && Unquote(e.Properties["To"].ToString()) == to);

    private static string Unquote(string value) => value == "null" ? "" : value.Trim('"');
}

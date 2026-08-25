using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T033 — GET /api/v1/me/communities (User Story 3). The intentional cross-community
/// surface: returns only communities where the caller holds an ACTIVE membership, as a
/// standard { items, total, limit, offset } envelope, summary fields only.
/// </summary>
public class MyCommunitiesEndpointTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    private async Task<JsonElement> GetAsync(string query = "")
    {
        var res = await Client.GetAsync("/api/v1/me/communities" + query);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    // US3: only communities with an ACTIVE membership are returned; inactive
    // memberships and communities-without-membership are excluded.
    [Fact]
    public async Task ReturnsOnlyCommunitiesWithActiveMembership()
    {
        string email;
        Guid active, inactiveMembership, noMembership;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            active = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, active);
            email = e;
            await AddMembershipAsync(db, userId, active, CommunityRole.BoardMember);

            inactiveMembership = await CreateCommunityAsync(db);
            await AddMembershipAsync(db, userId, inactiveMembership, CommunityRole.BoardMember,
                MembershipStatus.Inactive);

            noMembership = await CreateCommunityAsync(db);
        }

        await LoginAsync(email);
        var body = await GetAsync();

        // Response shape.
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.True(body.TryGetProperty("total", out _));
        Assert.True(body.TryGetProperty("limit", out _));
        Assert.True(body.TryGetProperty("offset", out _));

        var ids = items.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(active, ids);
        Assert.DoesNotContain(inactiveMembership, ids);
        Assert.DoesNotContain(noMembership, ids);
    }

    // US3: a user with zero board memberships gets an empty items array (they can still
    // log in via their property, but hold no communities).
    [Fact]
    public async Task NoBoardMemberships_ReturnsEmptyItems()
    {
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            var communityId = await CreateCommunityAsync(db);
            var (_, e, _) = await CreateUserWithPropertyAsync(db, communityId); // property only, no membership
            email = e;
        }

        await LoginAsync(email);
        var body = await GetAsync();

        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("total").GetInt32());
    }

    // US3: limit/offset pagination boundary — three active memberships, page size 1.
    [Fact]
    public async Task Pagination_LimitAndOffset_AreHonoured()
    {
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            var home = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, home);
            email = e;
            await AddMembershipAsync(db, userId, home, CommunityRole.BoardMember);
            await AddMembershipAsync(db, userId, await CreateCommunityAsync(db), CommunityRole.CommunityManager);
            await AddMembershipAsync(db, userId, await CreateCommunityAsync(db), CommunityRole.Accountant);
        }

        await LoginAsync(email);
        var body = await GetAsync("?limit=1&offset=1");

        Assert.Equal(3, body.GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("limit").GetInt32());
        Assert.Equal(1, body.GetProperty("offset").GetInt32());
        Assert.Single(body.GetProperty("items").EnumerateArray());
    }
}

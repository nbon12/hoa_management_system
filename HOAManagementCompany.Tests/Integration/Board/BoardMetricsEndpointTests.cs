using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T052 — GET /api/v1/board/metrics (User Story 4). Requires an active board-side
/// membership; the registry ships empty in this spec, so an empty items array is the
/// valid empty state (Edge Cases), not an error. Out-of-scope callers fail closed (403).
/// </summary>
public class BoardMetricsEndpointTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    private static string Url(Guid communityId, string surface = "CommunityMetrics", string extra = "") =>
        $"/api/v1/board/metrics?communityId={communityId}&surface={surface}{extra}";

    // US4 / Edge Cases: an active board member gets 200 with an empty items array
    // (the empty registry is a valid empty state).
    [Fact]
    public async Task ActiveBoardMembership_ReturnsEmptyItems_200()
    {
        Guid communityId;
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            email = e;
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(email);
        var res = await Client.GetAsync(Url(communityId));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("total").GetInt32());
    }

    // US2 / FR-016: a caller with no membership in the community is denied (403 FORBIDDEN),
    // fail-closed without disclosing whether the community exists.
    [Fact]
    public async Task NoMembership_IsForbidden_403()
    {
        Guid communityId;
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            // login-capable user, but NO membership in communityId
            var (_, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            email = e;
        }

        await LoginAsync(email);
        var res = await Client.GetAsync(Url(communityId));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString());
    }

    // US4: limit/offset pagination boundary — still an empty page over the empty registry.
    [Fact]
    public async Task Pagination_LimitAndOffset_ReturnsEmptyPage_200()
    {
        Guid communityId;
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            email = e;
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(email);
        var res = await Client.GetAsync(Url(communityId, extra: "&limit=10&offset=5"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(10, body.GetProperty("limit").GetInt32());
        Assert.Equal(5, body.GetProperty("offset").GetInt32());
    }
}

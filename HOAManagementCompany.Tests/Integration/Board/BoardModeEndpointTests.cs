using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T020 — POST /api/v1/auth/board-mode (User Story 1). The server independently
/// verifies an active non-resident membership from persisted state (FR-014/FR-020)
/// and persists the last-used mode across sessions (FR-022).
/// </summary>
public class BoardModeEndpointTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    private async Task<HttpResponseMessage> EnterBoardModeAsync() =>
        await Client.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" });

    // US1 Scenario 1/3: a user with an active non-resident membership can switch to
    // Board mode → 200, and the returned user reflects Board with the membership present.
    [Fact]
    public async Task ActiveBoardMembership_SwitchesToBoardMode_200()
    {
        string email;
        Guid communityId;
        string userId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            (userId, email, _) = await CreateUserWithPropertyAsync(db, communityId);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(email);
        var res = await EnterBoardModeAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        var user = body.GetProperty("user");
        Assert.Equal("Board", user.GetProperty("lastActiveMode").GetString());
        Assert.Contains(user.GetProperty("memberships").EnumerateArray(),
            m => m.GetProperty("communityId").GetGuid() == communityId
              && m.GetProperty("role").GetString() == "BoardMember");
    }

    // US1 Scenario 2 (server side): a resident-only user requesting Board mode is
    // denied with 403 NO_ACTIVE_MEMBERSHIP.
    [Fact]
    public async Task ResidentOnly_RequestingBoardMode_403_NoActiveMembership()
    {
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            var communityId = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            email = e;
            // Only a Resident membership — confers no board access.
            await AddMembershipAsync(db, userId, communityId, CommunityRole.Resident);
        }

        await LoginAsync(email);
        var res = await EnterBoardModeAsync();

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("NO_ACTIVE_MEMBERSHIP", body.GetProperty("code").GetString());
    }

    // US1 Scenario 5 (server side): a user whose only board membership has expired is
    // treated as holding none → 403 NO_ACTIVE_MEMBERSHIP.
    [Fact]
    public async Task ExpiredMembership_RequestingBoardMode_403()
    {
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            var communityId = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            email = e;
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember,
                startDate: Today.AddYears(-1), endDate: Today.AddDays(-1));
        }

        await LoginAsync(email);
        var res = await EnterBoardModeAsync();

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("NO_ACTIVE_MEMBERSHIP", body.GetProperty("code").GetString());
    }

    // US1 Scenario 4: after switching to Board, a fresh login returns lastActiveMode ==
    // Board (the last-used mode persists across sessions — FR-022).
    [Fact]
    public async Task ModePersistsAcrossSessions_FreshLoginReturnsBoard()
    {
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            var communityId = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            email = e;
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await EnterBoardModeAsync()).StatusCode);

        // Fresh sign-in (new token) — the persisted mode must come back as Board.
        Client.DefaultRequestHeaders.Authorization = null;
        var loginRes = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        var body = await loginRes.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Board", body.GetProperty("user").GetProperty("lastActiveMode").GetString());
    }
}

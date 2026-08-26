using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T046 — the membership-admin surface (User Story 5, FR-042). Community Manager only,
/// via the shared resolver; every create/edit is a "BoardMembershipChange" sensitive
/// Serilog event; the last active Community Manager cannot be ended or downgraded.
/// </summary>
public class MembershipAdminEndpointsTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    private static string Url(Guid communityId) => $"/api/v1/communities/{communityId}/memberships";
    private static string Url(Guid communityId, Guid membershipId) => $"{Url(communityId)}/{membershipId}";

    private async Task<bool> ResolveAsync(string userId, Guid communityId, CommunityCapability cap)
    {
        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        return await resolver.CanAccessAsync(Principal(userId), communityId, cap);
    }

    private bool HasSensitiveEvent(string prefix, Guid communityId, string actorId, string targetUserId) =>
        LogSink.Events.Any(e =>
            e.MessageTemplate.Text.StartsWith("BoardMembershipChange", StringComparison.Ordinal)
            && e.MessageTemplate.Text.Contains(prefix, StringComparison.Ordinal)
            && ScalarEquals(e, "CommunityId", communityId)
            && ScalarEquals(e, "ActorId", actorId)
            && ScalarEquals(e, "TargetUserId", targetUserId));

    private static bool ScalarEquals(LogEvent e, string property, object expected) =>
        e.Properties.TryGetValue(property, out var v)
        && v is ScalarValue sv
        && sv.Value is not null
        && sv.Value.ToString() == expected.ToString();

    // Sets up a Community Manager (login-capable) plus a login-capable target resident,
    // both in a fresh active community. Returns their identifiers.
    private async Task<(Guid communityId, string managerId, string managerEmail, string targetId, string targetEmail)> SetupManagerAndTargetAsync()
    {
        using var scope = NewScope();
        var db = Db(scope);
        var communityId = await CreateCommunityAsync(db);
        var (managerId, managerEmail, _) = await CreateUserWithPropertyAsync(db, communityId);
        await AddMembershipAsync(db, managerId, communityId, CommunityRole.CommunityManager);
        var (targetId, targetEmail, _) = await CreateUserWithPropertyAsync(db, communityId);
        return (communityId, managerId, managerEmail, targetId, targetEmail);
    }

    // US5 Scenario 1 + SC-009: a manager grants a Board Member membership → 201, active
    // immediately, and the target can then enter board mode. Create emits the sensitive event.
    [Fact]
    public async Task Manager_CreatesMembership_201_ActiveImmediately_TargetCanEnterBoardMode()
    {
        var s = await SetupManagerAndTargetAsync();

        await LoginAsync(s.managerEmail);
        var res = await Client.PostAsJsonAsync(Url(s.communityId),
            new { userId = s.targetId, role = (int)CommunityRole.BoardMember, startDate = Today, endDate = (DateOnly?)null });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("BoardMember", dto.GetProperty("role").GetString());
        Assert.Equal("Active", dto.GetProperty("status").GetString());

        // Sensitive event (FR-042 / §7): actor, community, target user.
        Assert.True(HasSensitiveEvent("grant", s.communityId, s.managerId, s.targetId));

        // Active immediately: the target can now enter board mode.
        Client.DefaultRequestHeaders.Authorization = null;
        await LoginAsync(s.targetEmail);
        var boardMode = await Client.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" });
        Assert.Equal(HttpStatusCode.OK, boardMode.StatusCode);
    }

    // US2 Scenario 1 / FR-011 + FR-012: record-level scoping. A manager of community A
    // sees ONLY community A's membership rows — community B's rows never appear, even
    // though the caller is authenticated and authorized for A. This is also the
    // endpoint's success-path coverage, so it pins the paged envelope
    // (items/total/limit/offset) and the MembershipDto shape.
    [Fact]
    public async Task Manager_ListsMemberships_ReturnsOnlyRouteCommunityRecords()
    {
        var managerStartDate = Today.AddDays(-30);
        Guid communityA;
        Guid managerMembershipId;
        string managerId;
        string managerEmail;
        var communityAUserIds = new List<string>();

        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityA = await CreateCommunityAsync(db);

            (managerId, managerEmail, _) = await CreateUserWithPropertyAsync(db, communityA);
            managerMembershipId = await AddMembershipAsync(
                db, managerId, communityA, CommunityRole.CommunityManager, startDate: managerStartDate);
            communityAUserIds.Add(managerId);

            var boardMemberId = await CreateUserAsync(db);
            await AddMembershipAsync(db, boardMemberId, communityA, CommunityRole.BoardMember);
            communityAUserIds.Add(boardMemberId);

            var accountantId = await CreateUserAsync(db);
            await AddMembershipAsync(db, accountantId, communityA, CommunityRole.Accountant);
            communityAUserIds.Add(accountantId);

            // A separate community whose rows must never leak into A's roster.
            var communityB = await CreateCommunityAsync(db);
            await AddMembershipAsync(db, await CreateUserAsync(db), communityB, CommunityRole.CommunityManager);
            await AddMembershipAsync(db, await CreateUserAsync(db), communityB, CommunityRole.BoardMember);
        }

        await LoginAsync(managerEmail);
        var res = await Client.GetAsync(Url(communityA));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);

        // Paged envelope (constitution §4/§5): items/total/limit/offset.
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(communityAUserIds.Count, body.GetProperty("total").GetInt32());
        Assert.Equal(Paging.DefaultLimit, body.GetProperty("limit").GetInt32());
        Assert.Equal(0, body.GetProperty("offset").GetInt32());

        // Record-level scoping: exactly A's rows — not one row more (B's, or any other
        // community's), not one fewer.
        Assert.Equal(communityAUserIds.Count, items.GetArrayLength());
        var returnedUserIds = items.EnumerateArray()
            .Select(i => i.GetProperty("userId").GetString()!)
            .ToList();
        Assert.Equal(
            communityAUserIds.OrderBy(id => id, StringComparer.Ordinal),
            returnedUserIds.OrderBy(id => id, StringComparer.Ordinal));

        // MembershipDto shape, read off the manager's own row.
        var managerRow = items.EnumerateArray()
            .Single(i => i.GetProperty("userId").GetString() == managerId);
        Assert.Equal(managerMembershipId, managerRow.GetProperty("id").GetGuid());
        Assert.Equal("Board Tester", managerRow.GetProperty("userDisplayName").GetString());
        Assert.Equal("CommunityManager", managerRow.GetProperty("role").GetString());
        Assert.Equal("Active", managerRow.GetProperty("status").GetString());
        Assert.Equal(managerStartDate, DateOnly.ParseExact(
            managerRow.GetProperty("startDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal(JsonValueKind.Null, managerRow.GetProperty("endDate").ValueKind);
    }

    // US5 Scenario 5 / FR-027-028: a non-manager (board member) is refused on every
    // membership-admin verb with 403 FORBIDDEN.
    [Fact]
    public async Task NonManager_IsForbidden_OnAllVerbs()
    {
        Guid communityId;
        Guid membershipId;
        string boardEmail;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            var (boardId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            boardEmail = e;
            await AddMembershipAsync(db, boardId, communityId, CommunityRole.BoardMember);
            var (otherId, _, _) = await CreateUserWithPropertyAsync(db, communityId);
            membershipId = await AddMembershipAsync(db, otherId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(boardEmail);

        var get = await Client.GetAsync(Url(communityId));
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal("FORBIDDEN", (await get.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString());

        var post = await Client.PostAsJsonAsync(Url(communityId),
            new { userId = Guid.NewGuid().ToString(), role = (int)CommunityRole.BoardMember, startDate = Today, endDate = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        var patch = await Client.PatchAsJsonAsync(Url(communityId, membershipId),
            new { status = (int)MembershipStatus.Inactive });
        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
    }

    // US5 Scenario 4 + FR-023: a manager ends a membership (end date in the past); the
    // member loses scope on the resolver's next check. Edit emits the sensitive event.
    [Fact]
    public async Task Manager_EndsMembership_MemberDeniedOnNextResolverCheck()
    {
        var s = await SetupManagerAndTargetAsync();
        Guid membershipId;
        using (var scope = NewScope())
            membershipId = await AddMembershipAsync(Db(scope), s.targetId, s.communityId, CommunityRole.BoardMember);

        // Sanity: active before the edit.
        Assert.True(await ResolveAsync(s.targetId, s.communityId, CommunityCapability.ViewAssociationData));

        await LoginAsync(s.managerEmail);
        var patch = await Client.PatchAsJsonAsync(Url(s.communityId, membershipId),
            new { endDate = Today.AddDays(-1) });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        Assert.True(HasSensitiveEvent("edit", s.communityId, s.managerId, s.targetId));

        // FR-023: denied on the very next request, no re-authentication.
        Assert.False(await ResolveAsync(s.targetId, s.communityId, CommunityCapability.ViewAssociationData));
    }

    // FR-042 validation: role must not be Resident, end date must not precede start
    // date, and the user must exist — each is a 422 VALIDATION_ERROR.
    [Fact]
    public async Task Create_InvalidInputs_Return422()
    {
        var s = await SetupManagerAndTargetAsync();
        await LoginAsync(s.managerEmail);

        var residentRole = await Client.PostAsJsonAsync(Url(s.communityId),
            new { userId = s.targetId, role = (int)CommunityRole.Resident, startDate = Today, endDate = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, residentRole.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await residentRole.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString());

        var badDates = await Client.PostAsJsonAsync(Url(s.communityId),
            new { userId = s.targetId, role = (int)CommunityRole.BoardMember, startDate = Today, endDate = Today.AddDays(-5) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badDates.StatusCode);

        var unknownUser = await Client.PostAsJsonAsync(Url(s.communityId),
            new { userId = "no-such-user-" + Guid.NewGuid().ToString("N"), role = (int)CommunityRole.BoardMember, startDate = Today, endDate = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownUser.StatusCode);
    }

    // FR-042: a membership id that does not belong to the route community is 404 NOT_FOUND.
    [Fact]
    public async Task Patch_MembershipNotInCommunity_Returns404()
    {
        var s = await SetupManagerAndTargetAsync();
        await LoginAsync(s.managerEmail);

        var res = await Client.PatchAsJsonAsync(Url(s.communityId, Guid.NewGuid()),
            new { status = (int)MembershipStatus.Inactive });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("NOT_FOUND", (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString());
    }

    // FR-042 / constitution §3: ending the ONLY active Community Manager is refused with
    // 422 LAST_MANAGER.
    [Fact]
    public async Task EndingLastManager_Returns422_LastManager()
    {
        Guid communityId;
        Guid managerMembershipId;
        string managerEmail;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            var (managerId, e, _) = await CreateUserWithPropertyAsync(db, communityId);
            managerEmail = e;
            managerMembershipId = await AddMembershipAsync(db, managerId, communityId, CommunityRole.CommunityManager);
        }

        await LoginAsync(managerEmail);
        var res = await Client.PatchAsJsonAsync(Url(communityId, managerMembershipId),
            new { endDate = Today.AddDays(-1) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        Assert.Equal("LAST_MANAGER", (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString());
    }

    // US5 Scenario 3 + positive control for the last-manager guard: downgrading a manager
    // is allowed when a SECOND active manager remains (proves the refusal is specific to
    // the last manager), and the capability MOVES with the role on the very next resolver
    // check — the downgraded user loses ManageMemberships and keeps ViewAssociationData.
    [Fact]
    public async Task DowngradingManager_Allowed_WhenAnotherManagerRemains()
    {
        Guid communityId;
        Guid manager2MembershipId;
        string manager1Email;
        string m2;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            var (m1, e1, _) = await CreateUserWithPropertyAsync(db, communityId);
            manager1Email = e1;
            await AddMembershipAsync(db, m1, communityId, CommunityRole.CommunityManager);
            (m2, _, _) = await CreateUserWithPropertyAsync(db, communityId);
            manager2MembershipId = await AddMembershipAsync(db, m2, communityId, CommunityRole.CommunityManager);
        }

        // Sanity: as a manager, m2 holds the membership-admin capability before the edit.
        Assert.True(await ResolveAsync(m2, communityId, CommunityCapability.ManageMemberships));

        await LoginAsync(manager1Email);
        var res = await Client.PatchAsJsonAsync(Url(communityId, manager2MembershipId),
            new { role = (int)CommunityRole.BoardMember });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("BoardMember", (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("role").GetString());

        // US5 Scenario 3 (FR-023): the write is not enough — the capability itself must
        // have moved. m2 is now a board member: no membership admin, but still association
        // data, with no re-authentication.
        Assert.False(await ResolveAsync(m2, communityId, CommunityCapability.ManageMemberships));
        Assert.True(await ResolveAsync(m2, communityId, CommunityCapability.ViewAssociationData));
    }
}

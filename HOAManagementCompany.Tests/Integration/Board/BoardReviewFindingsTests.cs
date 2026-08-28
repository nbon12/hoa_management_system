using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

// Coverage for gaps surfaced in review: FR-023 (expired board term returns to resident
// mode — the lockout bug), FR-017 (association-wide data-access audit), and the
// cross-tenant substitution / FR-016 non-disclosure cases that a "has any membership"
// resolver bug would slip past.
public class BoardReviewFindingsTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    // FR-023 / US1 Scenario 5: a board member whose term expires is served resident mode
    // on their next request — not stranded in a board shell with no exit.
    [Fact]
    public async Task ExpiredBoardTerm_ReportsResidentMode_OnNextLogin()
    {
        Guid communityId;
        string userId, email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            (userId, email, _) = await CreateUserWithPropertyAsync(db, communityId);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(email);
        var boarded = await Client.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" });
        boarded.EnsureSuccessStatusCode();
        Assert.Equal("Board", await ModeOfAsync(boarded));

        // Term ends.
        using (var scope = NewScope())
        {
            var db = Db(scope);
            var m = await db.CommunityMemberships.FirstAsync(x => x.UserId == userId && x.CommunityId == communityId);
            m.EndDate = Today.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var fresh = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        fresh.EnsureSuccessStatusCode();
        var body = await fresh.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Resident", body.GetProperty("user").GetProperty("lastActiveMode").GetString());
        Assert.Empty(body.GetProperty("user").GetProperty("memberships").EnumerateArray());
    }

    // FR-017 / US2 Scenario 5: accessing association-wide data emits a structured
    // sensitive event carrying actor, community, resource, and UTC timestamp.
    [Fact]
    public async Task AssociationDataAccess_EmitsAuditEvent_WithActorCommunityResource()
    {
        Guid communityId;
        string userId, email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            (userId, email, _) = await CreateUserWithPropertyAsync(db, communityId);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
        }

        await LoginAsync(email);
        var res = await Client.GetAsync($"/api/v1/board/metrics?communityId={communityId}&surface=CommunityMetrics");
        res.EnsureSuccessStatusCode();

        Assert.Contains(LogSink.Events, e =>
            e.MessageTemplate.Text.StartsWith("BoardAssociationDataAccess", StringComparison.Ordinal)
            && ScalarEquals(e, "ActorId", userId)
            && ScalarEquals(e, "CommunityId", communityId)
            && e.Properties.ContainsKey("Resource")
            && e.Properties.ContainsKey("UtcNow"));
    }

    // Cross-tenant substitution at the resolver: a caller who IS board-eligible in
    // community A is still denied community B (per-row scope, not "has any membership").
    [Fact]
    public async Task BoardEligibleInOneCommunity_DeniedForAnother()
    {
        using var scope = NewScope();
        var db = Db(scope);
        var a = await CreateCommunityAsync(db);
        var b = await CreateCommunityAsync(db);
        var userId = await CreateUserAsync(db);
        await AddMembershipAsync(db, userId, a, CommunityRole.BoardMember);

        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        Assert.True(await resolver.CanAccessAsync(Principal(userId), a, CommunityCapability.ViewAssociationData));
        Assert.False(await resolver.CanAccessAsync(Principal(userId), b, CommunityCapability.ViewAssociationData));
    }

    // FR-016 / US2 Scenario 2: an out-of-scope-but-existing community and a
    // never-existed community return an IDENTICAL 403 body — the denial never
    // discloses whether the community exists, and never 404s.
    [Fact]
    public async Task OutOfScopeAndNonexistentCommunity_ReturnIdenticalForbidden()
    {
        Guid inScope, outOfScope;
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            inScope = await CreateCommunityAsync(db);
            outOfScope = await CreateCommunityAsync(db);
            var (userId, e, _) = await CreateUserWithPropertyAsync(db, inScope);
            await AddMembershipAsync(db, userId, inScope, CommunityRole.BoardMember);
            email = e;
        }

        await LoginAsync(email);
        var existsButDenied = await Client.GetAsync($"/api/v1/board/metrics?communityId={outOfScope}&surface=CommunityMetrics");
        var neverExisted = await Client.GetAsync($"/api/v1/board/metrics?communityId={Guid.NewGuid()}&surface=CommunityMetrics");

        Assert.Equal(HttpStatusCode.Forbidden, existsButDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, neverExisted.StatusCode);
        Assert.Equal(
            await existsButDenied.Content.ReadAsStringAsync(),
            await neverExisted.Content.ReadAsStringAsync());
    }

    // US5 Scenario 2: a manager of community A cannot create a membership in community B.
    [Fact]
    public async Task ManagerOfOneCommunity_CannotAdministerAnother()
    {
        Guid managed, other;
        string managerEmail, targetUserId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            managed = await CreateCommunityAsync(db);
            other = await CreateCommunityAsync(db);
            var (managerId, e, _) = await CreateUserWithPropertyAsync(db, managed);
            await AddMembershipAsync(db, managerId, managed, CommunityRole.CommunityManager);
            managerEmail = e;
            targetUserId = await CreateUserAsync(db);
        }

        await LoginAsync(managerEmail);
        var res = await Client.PostAsJsonAsync(
            $"/api/v1/communities/{other}/memberships",
            new { userId = targetUserId, role = (int)CommunityRole.BoardMember, startDate = Today });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private static async Task<string?> ModeOfAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("user").GetProperty("lastActiveMode").GetString();
    }

    private static bool ScalarEquals(LogEvent e, string property, object expected) =>
        e.Properties.TryGetValue(property, out var v)
        && v is ScalarValue sv
        && sv.Value?.ToString() == expected.ToString();
}

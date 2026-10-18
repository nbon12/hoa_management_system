using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

/// <summary>
/// 030 T041 — sign-in without a usable home (FR-036a, FR-036b; research R11;
/// contracts/invitations-and-auth.md § Sign-in changes). A user with no home in an Active or
/// Archived community signs in with a token that carries no home, and <c>/auth/me</c> says where
/// the app goes next.
/// </summary>
public class SignInWithoutHomeTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private readonly FailRefreshMint _failRefreshMint = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.ConfigureDbContext<ApplicationDbContext>(o => o.AddInterceptors(_failRefreshMint));
    }

    // US9-S1
    [Fact]
    public async Task CompanyAdministrator_WithNoHome_SignsInToTheCompanyArea()
    {
        var companyId = await CreateCompanyAsync();
        var (_, email) = await CreateCompanyAdminAsync(companyId);

        var (body, _) = await LoginAsync(email);

        AssertNoHomeClaims(body.GetProperty("token").GetString()!);
        var me = await MeAsync(body);
        Assert.True(me.GetProperty("isCompanyAdministrator").GetBoolean());
        Assert.Equal("Company", me.GetProperty("landing").GetString());
        Assert.Empty(me.GetProperty("properties").EnumerateArray());
    }

    // US9-S2
    [Fact]
    public async Task ManagerOfAnOnboardingCommunity_WithNoHome_LandsOnTheBoard()
    {
        var companyId = await CreateCompanyAsync();
        var onboarding = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, email) = await CreateManagerAsync(onboarding);

        var (body, _) = await LoginAsync(email);

        AssertNoHomeClaims(body.GetProperty("token").GetString()!);
        var me = await MeAsync(body);
        Assert.Equal("Board", me.GetProperty("landing").GetString());
        Assert.Equal("Board", me.GetProperty("lastActiveMode").GetString());
        Assert.False(me.GetProperty("isCompanyAdministrator").GetBoolean());
        var membership = Assert.Single(me.GetProperty("memberships").EnumerateArray());
        Assert.Equal(onboarding, membership.GetProperty("communityId").GetGuid());
        Assert.Equal("CommunityManager", membership.GetProperty("role").GetString());
        Assert.Equal("Onboarding", membership.GetProperty("communityStatus").GetString());

        using var client = ClientWithToken(body.GetProperty("token").GetString()!);
        var mode = await client.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" });
        Assert.Equal(HttpStatusCode.OK, mode.StatusCode);
        var switched = await JsonAsync(mode);
        AssertNoHomeClaims(switched.GetProperty("token").GetString()!);
        Assert.Equal("Board", switched.GetProperty("user").GetProperty("lastActiveMode").GetString());

        var mine = await JsonAsync(await client.GetAsync("/api/v1/me/communities"));
        var item = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal(onboarding, item.GetProperty("id").GetGuid());
        Assert.Equal("Onboarding", item.GetProperty("status").GetString());
    }

    // US9-S3
    [Fact]
    public async Task NoHomeNoMembershipNoGrant_SignsInToNoHome()
    {
        var userId = await WithDbAsync(CreateUserAsync);

        var (body, _) = await LoginAsync(EmailOf(userId));

        AssertNoHomeClaims(body.GetProperty("token").GetString()!);
        var me = await MeAsync(body);
        Assert.Equal("NoHome", me.GetProperty("landing").GetString());
        Assert.False(me.GetProperty("isCompanyAdministrator").GetBoolean());
        Assert.Empty(me.GetProperty("properties").EnumerateArray());
        Assert.Empty(me.GetProperty("memberships").EnumerateArray());
    }

    // US9-S5
    [Fact]
    public async Task HomesOnlyInOnboardingCommunities_SignInToNotLive()
    {
        var companyId = await CreateCompanyAsync();
        var onboarding = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (userId, email) = await CreateResidentAsync(await CreatePropertyAsync(onboarding));
        await LinkResidentAsync(userId, await CreatePropertyAsync(onboarding));

        var (body, _) = await LoginAsync(email);

        AssertNoHomeClaims(body.GetProperty("token").GetString()!);
        var me = await MeAsync(body);
        Assert.Equal("NotLive", me.GetProperty("landing").GetString());
        Assert.Empty(me.GetProperty("properties").EnumerateArray());
    }

    // US9-S6
    [Fact]
    public async Task HomesInActiveAndArchivedCommunities_TokenCarriesTheActiveHome()
    {
        var companyId = await CreateCompanyAsync();
        var active = await CreateCommunityInAsync(companyId);
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var archivedHome = await CreatePropertyAsync(archived);
        var activeHome = await CreatePropertyAsync(active);
        // The Archived link is the older one, so only the status order can pick the Active home.
        var (userId, email) = await CreateResidentAsync(archivedHome);
        await LinkResidentAsync(userId, activeHome);

        var (body, _) = await LoginAsync(email);

        var claims = Claims(body.GetProperty("token").GetString()!);
        Assert.Equal(activeHome.ToString(), claims["propertyId"]);
        Assert.Equal(active.ToString(), claims["communityId"]);
        var me = await MeAsync(body);
        Assert.Equal("Resident", me.GetProperty("landing").GetString());
        var statuses = me.GetProperty("properties").EnumerateArray()
            .ToDictionary(p => p.GetProperty("id").GetGuid(), p => p.GetProperty("communityStatus").GetString());
        Assert.Equal(new Dictionary<Guid, string?> { [activeHome] = "Active", [archivedHome] = "Archived" }, statuses);
    }

    [Fact]
    public async Task OnlyAnArchivedHome_TokenCarriesTheArchivedHome()
    {
        var companyId = await CreateCompanyAsync();
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var home = await CreatePropertyAsync(archived);
        var (_, email) = await CreateResidentAsync(home);

        var (body, _) = await LoginAsync(email);

        Assert.Equal(home.ToString(), Claims(body.GetProperty("token").GetString()!)["propertyId"]);
        Assert.Equal("Resident", (await MeAsync(body)).GetProperty("landing").GetString());
    }

    [Fact]
    public async Task Refresh_ForAUserWithNoHome_RotatesTheToken()
    {
        var userId = await WithDbAsync(CreateUserAsync);
        var (_, cookie) = await LoginAsync(EmailOf(userId));

        var res = await CreateClient().SendAsync(Refresh(cookie));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        AssertNoHomeClaims(body.GetProperty("token").GetString()!);
        Assert.Equal("NoHome", body.GetProperty("user").GetProperty("landing").GetString());
        Assert.NotEqual(cookie, RefreshCookieOf(res));
    }

    [Fact]
    public async Task Refresh_FailingInsideItsTransaction_LeavesTheOldTokenUsable()
    {
        var userId = await WithDbAsync(CreateUserAsync);
        var (_, cookie) = await LoginAsync(EmailOf(userId));

        _failRefreshMint.Armed = true;
        var failed = await CreateClient().SendAsync(Refresh(cookie));
        Assert.False(_failRefreshMint.Armed, "the failure was never injected");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        var retry = await CreateClient().SendAsync(Refresh(cookie));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1, await WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null)));
    }

    [Fact]
    public async Task SwitchProperty_RefusesAnOnboardingHome_AndAllowsAnArchivedOne()
    {
        var companyId = await CreateCompanyAsync();
        var active = await CreateCommunityInAsync(companyId);
        var onboarding = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var (userId, email) = await CreateResidentAsync(await CreatePropertyAsync(active));
        var onboardingHome = await CreatePropertyAsync(onboarding);
        var archivedHome = await CreatePropertyAsync(archived);
        await LinkResidentAsync(userId, onboardingHome);
        await LinkResidentAsync(userId, archivedHome);
        using var client = await ClientForAsync(email);

        var toOnboarding = await client.PostAsJsonAsync("/api/v1/auth/switch-property", new { propertyId = onboardingHome });
        Assert.Equal(HttpStatusCode.Conflict, toOnboarding.StatusCode);
        Assert.Equal("COMMUNITY_NOT_ACTIVE", (await JsonAsync(toOnboarding)).GetProperty("code").GetString());

        var toArchived = await client.PostAsJsonAsync("/api/v1/auth/switch-property", new { propertyId = archivedHome });
        Assert.Equal(HttpStatusCode.OK, toArchived.StatusCode);
        var claims = Claims((await JsonAsync(toArchived)).GetProperty("token").GetString()!);
        Assert.Equal(archivedHome.ToString(), claims["propertyId"]);
        Assert.Equal(archived.ToString(), claims["communityId"]);
    }

    // FR-036b: memberships in Archived communities are left out of /auth/me and /me/communities.
    [Fact]
    public async Task ArchivedCommunities_AreLeftOutOfMembershipLists()
    {
        var companyId = await CreateCompanyAsync();
        var active = await CreateCommunityInAsync(companyId);
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var (userId, email) = await CreateManagerAsync(active);
        await WithDbAsync(db => AddMembershipAsync(db, userId, archived, CommunityRole.BoardMember));

        var (body, _) = await LoginAsync(email);

        var me = await MeAsync(body);
        var membership = Assert.Single(me.GetProperty("memberships").EnumerateArray());
        Assert.Equal(active, membership.GetProperty("communityId").GetGuid());
        Assert.Equal("Active", membership.GetProperty("communityStatus").GetString());

        using var client = ClientWithToken(body.GetProperty("token").GetString()!);
        var mine = await JsonAsync(await client.GetAsync("/api/v1/me/communities"));
        Assert.Equal([active], mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task BoardMode_IsRefused_WhenTheOnlyMembershipIsInAnArchivedCommunity()
    {
        var companyId = await CreateCompanyAsync();
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var (_, email) = await CreateStaffAsync(archived, CommunityRole.BoardMember);
        using var client = await ClientForAsync(email);

        var res = await client.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("NO_ACTIVE_MEMBERSHIP", (await JsonAsync(res)).GetProperty("code").GetString());
    }

    private async Task<(JsonElement Body, string RefreshCookie)> LoginAsync(string email)
    {
        var res = await CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await JsonAsync(res), RefreshCookieOf(res));
    }

    private async Task<JsonElement> MeAsync(JsonElement loginBody)
    {
        using var client = ClientWithToken(loginBody.GetProperty("token").GetString()!);
        var res = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await JsonAsync(res);
        // The AuthResponse's user and /auth/me agree on the landing.
        Assert.Equal(loginBody.GetProperty("user").GetProperty("landing").GetString(), me.GetProperty("landing").GetString());
        return me;
    }

    private static HttpRequestMessage Refresh(string cookie)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", cookie);
        return req;
    }

    private static string RefreshCookieOf(HttpResponseMessage res) =>
        res.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("neko_refresh=")).Split(';')[0];

    private static Dictionary<string, string> Claims(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.First().Value);

    private static void AssertNoHomeClaims(string token)
    {
        var claims = Claims(token);
        Assert.False(claims.ContainsKey("propertyId"), "the token carries a propertyId");
        Assert.False(claims.ContainsKey("communityId"), "the token carries a communityId");
    }

    /// <summary>
    /// Fails the first save that writes a refresh token once armed, after the rows reach the
    /// database and before the caller's transaction commits, so only that transaction can keep
    /// the old token alive.
    /// </summary>
    private sealed class FailRefreshMint : SaveChangesInterceptor
    {
        private volatile bool _armed;

        public bool Armed { get => _armed; set => _armed = value; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (_armed && eventData.Context!.ChangeTracker.Entries<RefreshToken>().Any())
            {
                _armed = false;
                throw new InvalidOperationException("Injected failure after the refresh token rows were written.");
            }
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }
}

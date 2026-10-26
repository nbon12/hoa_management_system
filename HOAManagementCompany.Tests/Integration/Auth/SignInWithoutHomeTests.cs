using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using HOAManagementCompany.Tests.Integration.Residents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

/// <summary>
/// 030 T041 — sign-in without a usable home (FR-036a, FR-036b; research R11;
/// contracts/invitations-and-auth.md § Sign-in changes). A user with no home in an Active or
/// Archived community signs in with a token that carries no home, <c>/auth/me</c> says where the
/// app goes next, and every resident endpoint refuses that token.
/// </summary>
public class SignInWithoutHomeTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum NoHomeCaller
    {
        NeverHadAHome,
        OnlyHomeRemoved,
        CommunityManager,
        CompanyAdministrator
    }

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

    // US9 Independent Test: a Board Member or an Accountant who never had a home signs in to board mode.
    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Accountant)]
    public async Task ABoardMemberOrAccountantWithNoHome_LandsOnTheBoard(CommunityRole role)
    {
        var active = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, email) = await CreateStaffAsync(active, role);

        var (body, _) = await LoginAsync(email);

        AssertNoHomeClaims(body.GetProperty("token").GetString()!);
        var me = await MeAsync(body);
        Assert.Equal("Board", me.GetProperty("landing").GetString());
        Assert.Empty(me.GetProperty("properties").EnumerateArray());
        var membership = Assert.Single(me.GetProperty("memberships").EnumerateArray());
        Assert.Equal((active, role.ToString()), (membership.GetProperty("communityId").GetGuid(), membership.GetProperty("role").GetString()));
        using var client = ClientWithToken(body.GetProperty("token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" })).StatusCode);
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

    // US9-S4: whoever has no linked home, every resident endpoint refuses the token sign-in issued
    // with PROPERTY_ACCESS_DENIED, and nothing is shown or changed.
    [Theory]
    [InlineData(NoHomeCaller.NeverHadAHome)]
    [InlineData(NoHomeCaller.OnlyHomeRemoved)]
    [InlineData(NoHomeCaller.CommunityManager)]
    [InlineData(NoHomeCaller.CompanyAdministrator)]
    public async Task NoLinkedHome_EveryResidentEndpointIsRefused_AndNothingIsShownOrChanged(NoHomeCaller caller)
    {
        var companyId = await CreateCompanyAsync();
        var active = await CreateCommunityInAsync(companyId);
        var home = await CreatePropertyAsync(active);
        var (announcementId, pollId, eventId) = await CreateCommunityContentAsync(active);
        var userId = caller switch
        {
            NoHomeCaller.NeverHadAHome => await WithDbAsync(CreateUserAsync),
            NoHomeCaller.OnlyHomeRemoved => await FormerResidentAsync(home),
            NoHomeCaller.CommunityManager => (await CreateManagerAsync(active)).UserId,
            NoHomeCaller.CompanyAdministrator => (await CreateCompanyAdminAsync(companyId)).UserId,
            _ => throw new ArgumentOutOfRangeException(nameof(caller)),
        };
        Assert.Equal(0, await WithDbAsync(db => db.UserProperties.CountAsync(up => up.UserId == userId)));

        var (body, _) = await LoginAsync(EmailOf(userId));
        var token = body.GetProperty("token").GetString()!;
        AssertNoHomeClaims(token);
        using var client = ClientWithToken(token);
        var before = await ResidentFingerprintAsync(userId, home);

        // Every row ResidentAccessTheoryTests covers, plus the community's real rows a fall-through
        // would show or write.
        (string Method, string Path, string? Body)[] calls =
        [
            .. ResidentAccessTheoryTests.Endpoints().Select(row => ((string)row[0], (string)row[1], (string?)row[3])),
            ("GET", $"/api/v1/community/announcements/{announcementId}", null),
            ("POST", $"/api/v1/community/poll/{pollId}/vote", """{"optionIndex":0}"""),
            ("POST", $"/api/v1/community/events/{eventId}/rsvp", """{"attending":true}"""),
        ];
        foreach (var (method, path, content) in calls)
        {
            using var res = await client.SendAsync(ResidentRequest(method, path, content));
            var text = await res.Content.ReadAsStringAsync();
            Assert.True(res.StatusCode == HttpStatusCode.Forbidden, $"{method} {path}: expected 403, got {(int)res.StatusCode} {text}");
            // Nothing is shown: the body is the refusal and no other member.
            var refusal = JsonDocument.Parse(text).RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
            Assert.True(refusal.Keys.Order().SequenceEqual(["code", "message"]) && refusal["code"] == "PROPERTY_ACCESS_DENIED",
                $"{method} {path}: expected only PROPERTY_ACCESS_DENIED, got {text}");
        }

        Assert.Equal(before, await ResidentFingerprintAsync(userId, home));
        Assert.Equal((0, 0, 0), (Stripe.CreatedIntents.Count, Stripe.CreatedSetupIntents.Count, Stripe.OffSessionCharges.Count));
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

    /// <summary>A resident whose only home link was removed.</summary>
    private async Task<string> FormerResidentAsync(Guid home)
    {
        var (userId, _) = await CreateResidentAsync(home);
        await WithDbAsync(db => db.UserProperties.Where(up => up.UserId == userId).ExecuteDeleteAsync());
        return userId;
    }

    /// <summary>An announcement, an open poll and an RSVP event, so a refusal that fell through would show or write one.</summary>
    private Task<(Guid Announcement, Guid Poll, Guid Event)> CreateCommunityContentAsync(Guid communityId) => WithDbAsync(async db =>
    {
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(), CommunityId = communityId, Title = "Pool reopens", Body = "Saturday at 10am.",
            PublishedAt = DateTimeOffset.UtcNow, Category = AnnouncementCategory.Events, AuthorName = "Board",
        };
        var poll = new Poll
        {
            Id = Guid.NewGuid(), CommunityId = communityId, Question = "Extend pool hours?", ClosingLabel = "closes Friday",
            Options = [new PollOption { Id = Guid.NewGuid(), OptionText = "Yes", OptionIndex = 0 }],
        };
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(), CommunityId = communityId, Title = "Pool party", EventDate = DateTimeOffset.UtcNow.AddDays(7),
            Category = EventCategory.Social, RsvpEnabled = true,
        };
        db.AddRange(announcement, poll, calendarEvent);
        await db.SaveChangesAsync();
        return (announcement.Id, poll.Id, calendarEvent.Id);
    });

    // Everything a resident endpoint could add or change for the caller and the community's home.
    private Task<string> ResidentFingerprintAsync(string userId, Guid home) => WithDbAsync(async db => string.Join("|",
        await db.UserProperties.CountAsync(up => up.UserId == userId || up.PropertyId == home),
        await db.PollVotes.CountAsync(v => v.UserId == userId),
        await db.EventRsvps.CountAsync(r => r.UserId == userId),
        await db.ArchitecturalApplicationDrafts.CountAsync(d => d.CreatedByUserId == userId || d.PropertyId == home),
        await db.RecurringPayments.CountAsync(r => r.EnrolledByUserId == userId || r.PropertyId == home),
        await db.PaymentTransactions.CountAsync(t => t.PropertyId == home),
        await db.DirectoryFields.CountAsync(f => f.PropertyId == home && f.Shared),
        await db.AlertConsents.CountAsync(c => c.Owner.PropertyId == home),
        await db.OutboxMessages.CountAsync(m => m.RecipientUserId == userId || db.Owners.Any(o => o.Id == m.OwnerId && o.PropertyId == home)),
        await db.Owners.Where(o => o.PropertyId == home)
            .Select(o => new { o.FirstName, o.Email, o.Phone, o.StripeCustomerId, o.AlertSmsOptIn, o.AlertEmailOptIn, o.UpdatedAt })
            .SingleAsync()));

    private static HttpRequestMessage ResidentRequest(string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body == "file")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n"));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "plans.pdf");
            request.Content = form;
        }
        else if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
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

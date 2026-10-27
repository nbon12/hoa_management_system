using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Invitations;

/// <summary>
/// 030 T052 — <c>POST /invitations/preview</c>, <c>/invitations/register</c> and
/// <c>/invitations/accept</c> (contracts/invitations-and-auth.md § Invitation links; FR-022,
/// FR-022a), with Company Administrator invitations.
/// </summary>
public partial class InvitationEndpointsTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    internal static partial Regex LinkToken();

    private const string StrongPassword = "Str0ng!Pass";

    [Fact]
    public async Task Preview_AValidLink_ReturnsTheOffer_AndTheInvitedEmailToAnAnonymousCaller()
    {
        var (companyId, _) = await CompanyWithAdminAsync("Juniper Row Community Management");
        var email = NewEmail();
        var (invitation, token) = await InviteAsync(companyId, email);

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/preview", new { token });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);
        var body = await JsonAsync(res);
        Assert.Equal("CompanyAdministrator", body.GetProperty("kind").GetString());
        Assert.Equal("Juniper Row Community Management", body.GetProperty("communityName").GetString());
        var offer = body.GetProperty("offer");
        Assert.Equal(JsonValueKind.Null, offer.GetProperty("role").ValueKind);
        Assert.Equal(JsonValueKind.Null, offer.GetProperty("termEndDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, offer.GetProperty("propertyAddress").ValueKind);
        Assert.Equal("Board Tester", body.GetProperty("sentBy").GetString());
        Assert.Equal(invitation.ExpiresAt, body.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal("America/New_York", body.GetProperty("timeZoneId").GetString());
        Assert.Equal(email, body.GetProperty("invitedEmail").GetString());
        Assert.False(body.GetProperty("hasAccount").GetBoolean());
    }

    [Fact]
    public async Task Preview_ForAnEmailWithAnAccount_SaysSo()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var userId = await WithDbAsync(CreateUserAsync);
        var (_, token) = await InviteAsync(companyId, EmailOf(userId));

        var body = await JsonAsync(await CreateClient().PostAsJsonAsync("/api/v1/invitations/preview", new { token }));

        Assert.True(body.GetProperty("hasAccount").GetBoolean());
    }

    // FR-022a: a signed-in caller (Bob on a shared laptop) never receives the invited email.
    [Fact]
    public async Task Preview_WhenSignedIn_LeavesOutTheInvitedEmailAndAccountFlag()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var email = NewEmail();
        var (_, token) = await InviteAsync(companyId, email);
        var bob = await WithDbAsync(CreateUserAsync);
        using var client = await ClientForAsync(EmailOf(bob));

        var res = await client.PostAsJsonAsync("/api/v1/invitations/preview", new { token });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("invitedEmail").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("hasAccount").ValueKind);
        Assert.DoesNotContain(email, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Preview_AnInvalidOrExpiredLink_ReturnsOnlyItsCode()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var (_, token) = await InviteAsync(companyId, NewEmail());

        var invalid = await CreateClient().PostAsJsonAsync("/api/v1/invitations/preview", new { token = InvitationTokens.New() });
        Clock.UtcNow = Clock.UtcNow.AddHours(72);
        var expired = await CreateClient().PostAsJsonAsync("/api/v1/invitations/preview", new { token });

        foreach (var (res, code) in new[] { (invalid, "INVITATION_INVALID"), (expired, "INVITATION_EXPIRED") })
        {
            Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
            Assert.True(res.Headers.CacheControl?.NoStore);
            var body = await JsonAsync(res);
            Assert.Equal(code, body.GetProperty("code").GetString());
            Assert.Equal(["code", "message"], body.EnumerateObject().Select(p => p.Name).Order());
        }
    }

    // US7-S1's account path (FR-022a): an account for the invited email, no claim code.
    [Fact]
    public async Task Register_CreatesTheAccount_AppliesTheOffer_AndSignsIn()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var email = NewEmail();
        var (invitation, token) = await InviteAsync(companyId, email);

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Pat", lastName = "Rivera", password = StrongPassword });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), c => c.StartsWith("neko_refresh="));
        var body = await JsonAsync(res);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.False(body.TryGetProperty("refreshToken", out _));
        var user = body.GetProperty("user");
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Company", user.GetProperty("landing").GetString());
        Assert.True(user.GetProperty("isCompanyAdministrator").GetBoolean());

        var created = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == email));
        Assert.True(created.EmailConfirmed);
        Assert.Equal(("Pat", "Rivera"), (created.FirstName, created.LastName));
        Assert.False(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == created.Id)));
        Assert.False(await WithDbAsync(db => db.PropertyClaimCodes.AnyAsync(c => c.RedeemedByUserId == created.Id)));
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.SingleAsync(g => g.UserId == created.Id));
        Assert.Equal((CompanyGrantSource.Invitation, invitation.Id), (grant.Source, grant.InvitationId));
        Assert.Equal(InvitationState.Accepted, (await ReloadAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Register_ForAnEmailThatHasAnAccount_IsTheGenericFailure_AndTheInvitationStaysPending()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var existing = await WithDbAsync(CreateUserAsync);
        var (invitation, token) = await InviteAsync(companyId, EmailOf(existing));

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Pat", lastName = "Rivera", password = StrongPassword });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("REGISTRATION_FAILED", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(InvitationState.Pending, (await ReloadAsync(invitation.Id)).State);
        Assert.False(await WithDbAsync(db => db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == existing)));
    }

    [Fact]
    public async Task Register_WithAWeakPassword_IsAValidationErrorOnThePassword_AndCreatesNothing()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var email = NewEmail();
        var (invitation, token) = await InviteAsync(companyId, email);

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Pat", lastName = "Rivera", password = "password" });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        Assert.All(body.GetProperty("errors").EnumerateArray(),
            e => Assert.Equal("password", e.GetProperty("field").GetString()));
        Assert.False(await WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
        Assert.Equal(InvitationState.Pending, (await ReloadAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Accept_AppliesTheOfferToTheCaller()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var userId = await WithDbAsync(CreateUserAsync);
        var (invitation, token) = await InviteAsync(companyId, EmailOf(userId));
        using var client = await ClientForAsync(EmailOf(userId));

        var res = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);
        var body = await JsonAsync(res);
        Assert.True(body.GetProperty("accepted").GetBoolean());
        Assert.False(body.GetProperty("alreadyHeld").GetBoolean());
        Assert.Equal("Company", body.GetProperty("landing").GetString());
        Assert.True(await WithDbAsync(db => db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == userId && g.RevokedAt == null)));
        Assert.Equal(InvitationState.Accepted, (await ReloadAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Accept_WhileAlreadyHoldingTheGrant_SaysSo_AndDuplicatesNothing()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var userId = await WithDbAsync(CreateUserAsync);
        var (invitation, token) = await InviteAsync(companyId, EmailOf(userId));
        await WithDbAsync(async db =>
        {
            db.CompanyAdministratorGrants.Add(new CompanyAdministratorGrant
            {
                Id = Guid.NewGuid(), CompanyId = companyId, UserId = userId, Source = CompanyGrantSource.Setup,
                GrantedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        using var client = await ClientForAsync(EmailOf(userId));

        var body = await JsonAsync(await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token }));

        Assert.True(body.GetProperty("accepted").GetBoolean());
        Assert.True(body.GetProperty("alreadyHeld").GetBoolean());
        Assert.Equal(1, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync(g => g.UserId == userId)));
        Assert.Equal(InvitationState.Accepted, (await ReloadAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Accept_ByADifferentAccount_Is403_WithoutTheInvitedEmail()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var email = NewEmail();
        var (invitation, token) = await InviteAsync(companyId, email);
        var bob = await WithDbAsync(CreateUserAsync);
        using var client = await ClientForAsync(EmailOf(bob));

        var res = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Equal("INVITATION_EMAIL_MISMATCH", JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(email, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InvitationState.Pending, (await ReloadAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Accept_Anonymously_Is401()
    {
        var (companyId, _) = await CompanyWithAdminAsync();
        var (_, token) = await InviteAsync(companyId, NewEmail());

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private async Task<(Guid CompanyId, string SenderId)> CompanyWithAdminAsync(string? name = null)
    {
        var companyId = await CreateCompanyAsync(name);
        var (senderId, _) = await CreateCompanyAdminAsync(companyId);
        return (companyId, senderId);
    }

    private async Task<(Invitation Invitation, string Token)> InviteAsync(Guid companyId, string email)
    {
        var senderId = await WithDbAsync(db => db.CompanyAdministratorGrants
            .Where(g => g.CompanyId == companyId && g.RevokedAt == null).Select(g => g.UserId).FirstAsync());
        using var scope = NewScope();
        var invitation = await scope.ServiceProvider.GetRequiredService<InvitationService>().CreateAsync(
            new NewInvitation(InvitationKind.CompanyAdministrator, companyId, null, null, null, null, email), senderId, default);
        var token = LinkToken().Match(Email.Sent.Last(m => m.Target == email).Body).Groups[1].Value;
        return (invitation, token);
    }

    private Task<Invitation> ReloadAsync(Guid id) =>
        WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == id));

    private static string NewEmail() => $"invitee-{Guid.NewGuid():N}@example.com";
}

/// <summary>030 T052 — all three invitation endpoints use the per-client <c>auth</c> rate limit (FR-041b).</summary>
public class InvitationEndpointsRateLimitTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const string EdgeHeader = "X-Edge-Auth";
    private const string EdgeSecret = "edge-secret-test";

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new Dictionary<string, string?>
    {
        ["RateLimiting:AuthPermitsPerMinute"] = "2",
        ["RateLimiting:UnknownPermitsPerMinute"] = "2",
        ["RateLimiting:TrustedEdge:SecretHeaderName"] = EdgeHeader,
        ["RateLimiting:TrustedEdge:SecretHeaderValue"] = EdgeSecret,
    };

    [Theory]
    [InlineData("/api/v1/invitations/preview", "203.0.113.11")]
    [InlineData("/api/v1/invitations/register", "203.0.113.12")]
    [InlineData("/api/v1/invitations/accept", "203.0.113.13")]
    public async Task ABurst_IsRateLimited(string path, string clientIp)
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new { token = InvitationTokens.New(), firstName = "A", lastName = "B", password = "Str0ng!Pass" }),
            };
            request.Headers.TryAddWithoutValidation(EdgeHeader, EdgeSecret);
            request.Headers.TryAddWithoutValidation("CF-Connecting-IP", clientIp);
            // Accept needs a signed-in caller; authorization runs before the rate limiter.
            if (path.EndsWith("/accept"))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", MintToken());
            statuses.Add((await Client.SendAsync(request)).StatusCode);
        }

        Assert.All(statuses.Take(2), s => Assert.Equal(HttpStatusCode.Gone, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
    }

    private string MintToken()
    {
        var config = Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString())], expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}

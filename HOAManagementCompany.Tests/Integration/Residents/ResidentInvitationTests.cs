using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T110 — inviting residents to a property by email (US8; FR-024–FR-024b, FR-022;
/// contracts/community-settings.md § Residents, contracts/invitations-and-auth.md).
/// </summary>
public partial class ResidentInvitationTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Address = "711 Keystone Park Dr #29";

    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    // US8-S1
    [Fact]
    public async Task AManagerInvitesJane_AndSheRegistersFromTheLink_WithNoClaimCode()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var jane = $"jane-{Guid.NewGuid():N}@example.com";

        var res = await InviteAsync(manager, community, home, jane);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("Pending", body.GetProperty("state").GetString());
        Assert.Equal(Clock.UtcNow.AddHours(72), body.GetProperty("expiresAt").GetDateTimeOffset());
        var sent = Assert.Single(Email.Sent, m => m.Target == jane);
        Assert.Contains(Address, sent.Subject + sent.Body);

        var register = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token = TokenIn(sent.Body), firstName = "Jane", lastName = "Alvarez", password = "Str0ng!Pass" });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == jane));
        Assert.True(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == user.Id && up.PropertyId == home)));
        using var janeClient = ClientWithToken((await JsonAsync(register)).GetProperty("token").GetString()!);
        var me = await JsonAsync(await janeClient.GetAsync("/api/v1/auth/me"));
        Assert.Equal("Resident", me.GetProperty("landing").GetString());
        Assert.Equal([home], me.GetProperty("properties").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()));
    }

    // US8-S2
    [Fact]
    public async Task ASecondInvitee_JoinsAHomeThatAlreadyHasAResident()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var (firstId, _) = await CreateResidentAsync(home);
        var secondId = await WithDbAsync(CreateUserAsync);
        Assert.Equal(HttpStatusCode.Created, (await InviteAsync(manager, community, home, EmailOf(secondId))).StatusCode);
        using var second = await ClientForAsync(EmailOf(secondId));

        var res = await second.PostAsJsonAsync("/api/v1/invitations/accept", new { token = TokensSentTo(EmailOf(secondId)).Single() });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var linked = await WithDbAsync(db => db.UserProperties.Where(up => up.PropertyId == home).Select(up => up.UserId).ToListAsync());
        Assert.Equal(new[] { firstId, secondId }.Order(), linked.Order());
    }

    // US8-S3
    [Fact]
    public async Task InvitingSomeoneWithAnAccount_LooksTheSame_AndLinksNothingUntilTheyAccept()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var otherHome = await CreatePropertyAsync(await CreateCommunityInAsync(await CreateCompanyAsync()));
        var (samId, samEmail) = await CreateResidentAsync(otherHome);
        var newcomer = $"new-{Guid.NewGuid():N}@example.com";

        var forSam = await InviteAsync(manager, community, home, samEmail);
        var forNewcomer = await InviteAsync(manager, community, await CreatePropertyAsync(community), newcomer);

        Assert.Equal(forNewcomer.StatusCode, forSam.StatusCode);
        Assert.Equal(Shape(await JsonAsync(forNewcomer)), Shape(await JsonAsync(forSam)));
        Assert.False(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == samId && up.PropertyId == home)));

        using var sam = await ClientForAsync(samEmail);
        var accept = await sam.PostAsJsonAsync("/api/v1/invitations/accept", new { token = TokensSentTo(samEmail).Single() });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var homes = (await MeAsync(samEmail)).GetProperty("properties").EnumerateArray().Select(p => p.GetProperty("id").GetGuid());
        Assert.Equal(new[] { home, otherHome }.Order(), homes.Order());
    }

    // US8-S4
    [Fact]
    public async Task InAnOnboardingCommunity_TheInvitationIsScheduled_AndSentWhenItGoesLive()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community, Address);
        using var manager = await ClientForAsync(managerEmail);
        var jane = $"jane-{Guid.NewGuid():N}@example.com";

        var res = await InviteAsync(manager, community, home, jane);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("Scheduled", JsonValueKind.Null), (body.GetProperty("state").GetString(), body.GetProperty("expiresAt").ValueKind));
        var id = body.GetProperty("invitationId").GetGuid();
        var row = await InvitationAsync(id);
        Assert.Equal((InvitationState.Scheduled, (string?)null, (DateTimeOffset?)null), (row.State, row.TokenHash, row.ExpiresAt));
        Assert.Empty(Email.Sent);

        Clock.UtcNow = Clock.UtcNow.AddMinutes(5);
        var resend = await manager.PostAsync($"/api/v1/communities/{community}/invitations/{id}/resend", null);
        Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
        Assert.Equal("COMMUNITY_NOT_ACTIVE", (await JsonAsync(resend)).GetProperty("code").GetString());
        Assert.Empty(Email.Sent);

        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        Clock.UtcNow = Clock.UtcNow.AddDays(2);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/activate", null)).StatusCode);

        row = await InvitationAsync(id);
        Assert.Equal((InvitationState.Pending, (DateTimeOffset?)Clock.UtcNow.AddHours(72)), (row.State, row.ExpiresAt));
        Assert.Single(Email.Sent, m => m.Target == jane);
    }

    // US8-S4 (a scheduled invitation whose sender lost authority)
    [Fact]
    public async Task AtActivation_AScheduledInvitationWhoseSenderLostAuthority_IsRevokedInstead()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        await CreateManagerAsync(community);
        var (formerId, formerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var jane = $"jane-{Guid.NewGuid():N}@example.com";
        using (var former = await ClientForAsync(formerEmail))
            Assert.Equal(HttpStatusCode.Created, (await InviteAsync(former, community, home, jane)).StatusCode);
        await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == formerId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Inactive)));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/activate", null)).StatusCode);

        var row = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.PropertyId == home));
        Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.SenderLostAuthority),
            (row.State, row.RevokeReason));
        Assert.DoesNotContain(Email.Sent, m => m.Target == jane);
    }

    // US8-S5
    [Fact]
    public async Task ALinkUsedAfter72Hours_IsExpired_AndAResendSendsANewOne()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var janeId = await WithDbAsync(CreateUserAsync);
        var body = await JsonAsync(await InviteAsync(manager, community, home, EmailOf(janeId)));
        var id = body.GetProperty("invitationId").GetGuid();
        var oldToken = TokensSentTo(EmailOf(janeId)).Single();
        Clock.UtcNow = Clock.UtcNow.AddHours(72).AddMinutes(1);
        using var jane = await ClientForAsync(EmailOf(janeId));

        var late = await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token = oldToken });

        Assert.Equal(HttpStatusCode.Gone, late.StatusCode);
        Assert.Equal("INVITATION_EXPIRED", (await JsonAsync(late)).GetProperty("code").GetString());
        Assert.False(await LinkedAsync(janeId, home));

        var resend = await manager.PostAsync($"/api/v1/communities/{community}/invitations/{id}/resend", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(Clock.UtcNow.AddHours(72), (await JsonAsync(resend)).GetProperty("expiresAt").GetDateTimeOffset());
        var newToken = TokensSentTo(EmailOf(janeId)).Last();
        Assert.NotEqual(oldToken, newToken);

        var old = await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token = oldToken });
        Assert.Equal(HttpStatusCode.Gone, old.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(old)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token = newToken })).StatusCode);
        Assert.True(await LinkedAsync(janeId, home));
    }

    // US8-S9
    [Fact]
    public async Task InvitingSomeoneWhoAlreadyLivesThere_IsAlreadyResident()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var (_, janeEmail) = await CreateResidentAsync(home);

        var res = await InviteAsync(manager, community, home, janeEmail.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("ALREADY_RESIDENT", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.PropertyId == home)));
        Assert.Empty(Email.Sent);
    }

    // US8-S10
    [Theory]
    [InlineData(CommunityStatus.Onboarding, "Scheduled")]
    [InlineData(CommunityStatus.Active, "Pending")]
    public async Task InvitingKimAgain_WhileHerInvitationIsOpen_IsInvitationPending(CommunityStatus status, string state)
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync(), status);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var kim = $"kim-{Guid.NewGuid():N}@example.com";
        var first = await JsonAsync(await InviteAsync(manager, community, home, kim));
        Assert.Equal(state, first.GetProperty("state").GetString());

        var again = await InviteAsync(manager, community, home, kim);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var body = await JsonAsync(again);
        Assert.Equal("INVITATION_PENDING", body.GetProperty("code").GetString());
        Assert.Equal(first.GetProperty("invitationId").GetGuid(), body.GetProperty("invitationId").GetGuid());
        Assert.Equal(1, await WithDbAsync(db => db.Invitations.CountAsync(i => i.PropertyId == home)));
    }

    // US8-S15
    [Fact]
    public async Task ARevokedInvitationsLink_IsInvalid()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var janeId = await WithDbAsync(CreateUserAsync);
        var id = (await JsonAsync(await InviteAsync(manager, community, home, EmailOf(janeId)))).GetProperty("invitationId").GetGuid();

        var revoke = await manager.PostAsync($"/api/v1/communities/{community}/invitations/{id}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        using var jane = await ClientForAsync(EmailOf(janeId));
        var res = await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token = TokensSentTo(EmailOf(janeId)).Single() });
        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.False(await LinkedAsync(janeId, home));
    }

    // US8-S16
    [Fact]
    public async Task ACompanyAdministratorWithNoMembership_InvitesAndLaterRemoves()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community, Address);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var janeId = await WithDbAsync(CreateUserAsync);

        var res = await InviteAsync(admin, community, home, EmailOf(janeId));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Single(Email.Sent, m => m.Target == EmailOf(janeId));
        using var jane = await ClientForAsync(EmailOf(janeId));
        Assert.Equal(HttpStatusCode.OK,
            (await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token = TokensSentTo(EmailOf(janeId)).Single() })).StatusCode);
        using var janeAtHome = ClientWithToken(MintToken(janeId, home, community));
        Assert.Equal(HttpStatusCode.OK, (await janeAtHome.GetAsync("/api/v1/property")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/communities/{community}/residents/{home}/{janeId}")).StatusCode);

        var next = await janeAtHome.GetAsync("/api/v1/property");
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Equal("PROPERTY_ACCESS_DENIED", (await JsonAsync(next)).GetProperty("code").GetString());
    }

    // US8-S17; US7-S10 (resident half)
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvitingYourOwnEmail_InAnyCase_IsASelfGrant(bool companyAdministrator)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        var (_, email) = companyAdministrator ? (adminId, adminEmail) : (managerId, managerEmail);
        var home = await CreatePropertyAsync(community);
        using var caller = await ClientForAsync(email);

        var res = await InviteAsync(caller, community, home, email.ToUpperInvariant());

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("SELF_GRANT_NOT_ALLOWED", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.PropertyId == home)));
    }

    [Fact]
    public async Task TheSameEmailInvitedToTwoProperties_GetsTwoIndependentInvitations()
    {
        var (community, home, manager) = await ActiveCommunityWithManagerAsync();
        var second = await CreatePropertyAsync(community);
        var jane = $"jane-{Guid.NewGuid():N}@example.com";

        var a = await JsonAsync(await InviteAsync(manager, community, home, jane));
        var b = await JsonAsync(await InviteAsync(manager, community, second, jane));
        var aId = a.GetProperty("invitationId").GetGuid();

        Assert.NotEqual(aId, b.GetProperty("invitationId").GetGuid());
        Assert.Equal(2, TokensSentTo(jane).Distinct().Count());
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/api/v1/communities/{community}/invitations/{aId}/revoke", null)).StatusCode);
        Assert.Equal(InvitationState.Pending, (await InvitationAsync(b.GetProperty("invitationId").GetGuid())).State);
    }

    [Fact]
    public async Task APropertyOutsideTheCommunity_GetsTheSame403_AsOneThatDoesntExist()
    {
        var (community, _, manager) = await ActiveCommunityWithManagerAsync();
        var elsewhere = await CreatePropertyAsync(await CreateCommunityInAsync(await CreateCompanyAsync()));

        var outside = await InviteAsync(manager, community, elsewhere, "kim@example.com");
        var missing = await InviteAsync(manager, community, Guid.NewGuid(), "kim@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(outside)).GetProperty("code").GetString());
        Assert.Equal(missing.StatusCode, outside.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await outside.Content.ReadAsStringAsync());
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.PropertyId == elsewhere)));
    }

    private async Task<(Guid Community, Guid Home, HttpClient Manager)> ActiveCommunityWithManagerAsync()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community, Address);
        return (community, home, await ClientForAsync(managerEmail));
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient sender, Guid community, Guid propertyId, string email) =>
        sender.PostAsJsonAsync($"/api/v1/communities/{community}/residents/invitations", new { propertyId, email });

    private static string TokenIn(string body) => LinkToken().Match(body).Groups[1].Value;

    private List<string> TokensSentTo(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => TokenIn(m.Body)).ToList();

    private Task<Invitation> InvitationAsync(Guid id) =>
        WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == id));

    private Task<bool> LinkedAsync(string userId, Guid home) =>
        WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == userId && up.PropertyId == home));

    // The response's fields and their kinds, without the values that differ per invitation.
    private static string Shape(JsonElement body) => string.Join(",", body.EnumerateObject()
        .Select(p => $"{p.Name}:{(p.Name == "state" ? p.Value.GetString() : p.Value.ValueKind.ToString())}"));

    private async Task<JsonElement> MeAsync(string email)
    {
        using var client = await ClientForAsync(email);
        return await JsonAsync(await client.GetAsync("/api/v1/auth/me"));
    }
}

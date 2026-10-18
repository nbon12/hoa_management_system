using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T081 — <c>GET /communities/{id}/invitations</c> and its resend and revoke routes
/// (contracts/community-settings.md § Invitations). The SC-005 archived-write rows for these
/// routes live in ArchivedCommunityWritesTheoryTests (T095).
/// </summary>
public partial class MembershipInvitationsListTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    // US5-S8
    [Fact]
    public async Task APendingBoardMemberAndAnExpiredManagerInvitation_AreBothListed_WithResendAndRevoke()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var alex = $"alex-{Guid.NewGuid():N}@example.com";
        var lee = $"lee-{Guid.NewGuid():N}@example.com";
        var expiredSentAt = Clock.UtcNow;
        await InviteAsync(manager, community, CommunityRole.CommunityManager, alex);
        Clock.UtcNow = Clock.UtcNow.AddHours(73);
        var pendingSentAt = Clock.UtcNow;
        await InviteAsync(manager, community, CommunityRole.BoardMember, lee);

        var res = await manager.GetAsync($"/api/v1/communities/{community}/invitations");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore);
        var body = await JsonAsync(res);
        Assert.Equal(2, body.GetProperty("total").GetInt32());
        Assert.Equal("America/New_York", body.GetProperty("timeZoneId").GetString());
        var items = body.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("email").GetString()!);
        foreach (var (email, role, state, sentAt) in new[]
                 {
                     (lee, "BoardMember", "Pending", pendingSentAt),
                     (alex, "CommunityManager", "Expired", expiredSentAt),
                 })
        {
            var item = items[email];
            Assert.Equal((role, state, "Board Tester", sentAt, sentAt.AddHours(72), true, true),
                (item.GetProperty("role").GetString(), item.GetProperty("effectiveState").GetString(),
                 item.GetProperty("sentBy").GetString(), item.GetProperty("sentAt").GetDateTimeOffset(),
                 item.GetProperty("expiresAt").GetDateTimeOffset(), item.GetProperty("canResend").GetBoolean(),
                 item.GetProperty("canRevoke").GetBoolean()));
        }
    }

    [Fact]
    public async Task Revoke_GivesRevokedManual_AndItsLinkIsInvalid()
    {
        var (community, manager) = await CommunityWithManagerAsync();
        var inviteeId = await WithDbAsync(CreateUserAsync);
        var (invitationId, token) = await InviteAsync(manager, community, CommunityRole.BoardMember, EmailOf(inviteeId));

        var res = await manager.PostAsync($"/api/v1/communities/{community}/invitations/{invitationId}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var item = await JsonAsync(res);
        Assert.Equal(("Revoked", "Manual", false, false),
            (item.GetProperty("state").GetString(), item.GetProperty("revokeReason").GetString(),
             item.GetProperty("canResend").GetBoolean(), item.GetProperty("canRevoke").GetBoolean()));
        using var invitee = await ClientForAsync(EmailOf(inviteeId));
        var accept = await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.Gone, accept.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(accept)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("revoked")]
    public async Task ResendingAnAcceptedOrRevokedInvitation_IsInvalid(string state)
    {
        var (community, manager) = await CommunityWithManagerAsync();
        var inviteeId = await WithDbAsync(CreateUserAsync);
        var (invitationId, token) = await InviteAsync(manager, community, CommunityRole.BoardMember, EmailOf(inviteeId));
        if (state == "accepted")
        {
            using var invitee = await ClientForAsync(EmailOf(inviteeId));
            Assert.Equal(HttpStatusCode.OK, (await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token })).StatusCode);
        }
        else
            Assert.Equal(HttpStatusCode.OK,
                (await manager.PostAsync($"/api/v1/communities/{community}/invitations/{invitationId}/revoke", null)).StatusCode);
        Clock.UtcNow = Clock.UtcNow.AddMinutes(5);

        var res = await manager.PostAsync($"/api/v1/communities/{community}/invitations/{invitationId}/resend", null);

        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(res)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnInvitationOfAnotherCommunity_IsForbidden()
    {
        var companyId = await CreateCompanyAsync();
        var a = await CreateCommunityInAsync(companyId);
        var b = await CreateCommunityInAsync(companyId);
        var (_, managerA) = await CreateManagerAsync(a);
        var (_, managerB) = await CreateManagerAsync(b);
        using var clientA = await ClientForAsync(managerA);
        using var clientB = await ClientForAsync(managerB);
        var (invitationInB, _) = await InviteAsync(clientB, b, CommunityRole.BoardMember, $"x-{Guid.NewGuid():N}@example.com");

        foreach (var action in new[] { "resend", "revoke" })
        {
            var res = await clientA.PostAsync($"/api/v1/communities/{a}/invitations/{invitationInB}/{action}", null);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.Equal(InvitationState.Pending,
            await WithDbAsync(db => db.Invitations.Where(i => i.Id == invitationInB).Select(i => i.State).SingleAsync()));
    }

    private async Task<(Guid Community, HttpClient Manager)> CommunityWithManagerAsync()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        return (community, await ClientForAsync(managerEmail));
    }

    private async Task<(Guid InvitationId, string Token)> InviteAsync(HttpClient sender, Guid community, CommunityRole role, string email)
    {
        var res = await sender.PostAsJsonAsync($"/api/v1/communities/{community}/memberships", new { role = (int)role, email });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (await JsonAsync(res)).GetProperty("invitationId").GetGuid();
        var token = LinkToken().Match(Email.Sent.Last(m => m.Target == email).Body).Groups[1].Value;
        return (id, token);
    }
}

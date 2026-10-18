using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Invitations;

/// <summary>
/// 030 T080 — accepting membership invitations (US2; FR-018, FR-021, FR-022, FR-022a).
/// </summary>
public partial class MembershipInvitationAcceptanceTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    // US2-S1 (accept)
    [Fact]
    public async Task PatAcceptsWithin72Hours_AndBecomesAnOpenEndedManagerFromToday()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Onboarding);
        var patId = await WithDbAsync(CreateUserAsync);
        var token = await InviteAsync(admin, community, CommunityRole.CommunityManager, EmailOf(patId));
        Clock.UtcNow = Clock.UtcNow.AddHours(71);
        using var pat = await ClientForAsync(EmailOf(patId));

        var res = await pat.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var membership = await MembershipAsync(community, patId, CommunityRole.CommunityManager);
        Assert.Equal((MembershipStatus.Active, DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime), (DateOnly?)null),
            (membership!.Status, membership.StartDate, membership.EndDate));
    }

    // US2-S2
    [Fact]
    public async Task PatRegistersFromTheLink_WithNoClaimCode_AndLandsInBoardMode()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Onboarding);
        var patEmail = $"pat-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(admin, community, CommunityRole.CommunityManager, patEmail);

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Pat", lastName = "Rivera", password = "Str0ng!Pass" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("Board", body.GetProperty("user").GetProperty("landing").GetString());
        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == patEmail));
        Assert.True(user.EmailConfirmed);
        Assert.NotNull(await MembershipAsync(community, user.Id, CommunityRole.CommunityManager));
        Assert.False(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == user.Id)));

        using var pat = ClientWithToken(body.GetProperty("token").GetString()!);
        var mode = await pat.PostAsJsonAsync("/api/v1/auth/board-mode", new { mode = "Board" });
        Assert.Equal(HttpStatusCode.OK, mode.StatusCode);
    }

    // US2-S5
    [Fact]
    public async Task ALinkUsed72HoursAnd1MinuteLater_IsExpired_AndNoMembershipIsCreated()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Onboarding);
        var patId = await WithDbAsync(CreateUserAsync);
        var token = await InviteAsync(admin, community, CommunityRole.CommunityManager, EmailOf(patId));
        Clock.UtcNow = Clock.UtcNow.AddHours(72).AddMinutes(1);
        using var pat = await ClientForAsync(EmailOf(patId));

        var res = await pat.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.Equal("INVITATION_EXPIRED", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await MembershipAsync(community, patId, CommunityRole.CommunityManager));
    }

    // US2-S6
    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    public async Task ResendingAPendingOrExpiredInvitation_SendsANewLinkFor72Hours(int hoursLater)
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Onboarding);
        var patId = await WithDbAsync(CreateUserAsync);
        var oldToken = await InviteAsync(admin, community, CommunityRole.CommunityManager, EmailOf(patId));
        var invitationId = await WithDbAsync(db => db.Invitations.Where(i => i.CommunityId == community).Select(i => i.Id).SingleAsync());
        Clock.UtcNow = Clock.UtcNow.AddHours(hoursLater);

        var resend = await admin.PostAsync($"/api/v1/communities/{community}/invitations/{invitationId}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var item = await JsonAsync(resend);
        Assert.Equal(("Pending", Clock.UtcNow.AddHours(72)),
            (item.GetProperty("effectiveState").GetString(), item.GetProperty("expiresAt").GetDateTimeOffset()));
        var newToken = TokensSentTo(EmailOf(patId)).Last();
        Assert.NotEqual(oldToken, newToken);
        using var pat = await ClientForAsync(EmailOf(patId));
        var old = await pat.PostAsJsonAsync("/api/v1/invitations/accept", new { token = oldToken });
        Assert.Equal(HttpStatusCode.Gone, old.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(old)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await pat.PostAsJsonAsync("/api/v1/invitations/accept", new { token = newToken })).StatusCode);
    }

    // US2-S7
    [Fact]
    public async Task BobSignedIn_AcceptingAnnsInvitation_IsAnEmailMismatch_WithoutAnnsEmail()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Active);
        var ann = $"ann-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(admin, community, CommunityRole.BoardMember, ann);
        var bobId = await WithDbAsync(CreateUserAsync);
        using var bob = await ClientForAsync(EmailOf(bobId));

        var res = await bob.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("INVITATION_EMAIL_MISMATCH", text);
        Assert.DoesNotContain("ann-", text);
        Assert.Null(await MembershipAsync(community, bobId, CommunityRole.BoardMember));
        Assert.Equal(InvitationState.Pending,
            await WithDbAsync(db => db.Invitations.Where(i => i.CommunityId == community).Select(i => i.State).SingleAsync()));
    }

    // US2-S15: the sender's removal revokes their invitation, and the link is then invalid.
    [Fact]
    public async Task ACoManagerIsRemovedAfterInviting_SoTheLinkIsInvalid()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (xId, xEmail) = await CreateManagerAsync(community);
        var inviteeId = await WithDbAsync(CreateUserAsync);
        using var x = await ClientForAsync(xEmail);
        var token = await InviteAsync(x, community, CommunityRole.BoardMember, EmailOf(inviteeId));
        var xRow = await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == xId).Select(m => m.Id).SingleAsync());
        using var manager = await ClientForAsync(managerEmail);
        Assert.Equal(HttpStatusCode.OK,
            (await manager.PatchAsJsonAsync($"/api/v1/communities/{community}/memberships/{xRow}", new { end = true })).StatusCode);

        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.CommunityId == community));
        Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.SenderLostAuthority),
            (invitation.State, invitation.RevokeReason));
        using var invitee = await ClientForAsync(EmailOf(inviteeId));
        var res = await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await MembershipAsync(community, inviteeId, CommunityRole.BoardMember));
    }

    // US2-S15: authority lost another way is caught when the link is used.
    [Fact]
    public async Task ASenderWhoLostAuthorityOtherwise_IsCaughtAtAcceptance()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        var (xId, xEmail) = await CreateManagerAsync(community);
        var inviteeId = await WithDbAsync(CreateUserAsync);
        using var x = await ClientForAsync(xEmail);
        var token = await InviteAsync(x, community, CommunityRole.BoardMember, EmailOf(inviteeId));
        // The term ends in the past, set directly: no endpoint ran, so nothing revoked the invitation.
        await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == xId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.EndDate, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))));
        using var invitee = await ClientForAsync(EmailOf(inviteeId));

        var res = await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(res)).GetProperty("code").GetString());
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.CommunityId == community));
        Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.SenderLostAuthority),
            (invitation.State, invitation.RevokeReason));
        Assert.Null(await MembershipAsync(community, inviteeId, CommunityRole.BoardMember));
    }

    // US2-S16 (accept)
    [Fact]
    public async Task ABoardMemberInvitationWithATerm_IsActiveFromAcceptanceToItsEnd()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Active);
        var inviteeId = await WithDbAsync(CreateUserAsync);
        var token = await InviteAsync(admin, community, CommunityRole.BoardMember, EmailOf(inviteeId), "2027-12-31");
        using var invitee = await ClientForAsync(EmailOf(inviteeId));

        Assert.Equal(HttpStatusCode.OK, (await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token })).StatusCode);

        var membership = await MembershipAsync(community, inviteeId, CommunityRole.BoardMember);
        Assert.Equal((MembershipStatus.Active, DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime), (DateOnly?)new DateOnly(2027, 12, 31)),
            (membership!.Status, membership.StartDate, membership.EndDate));
    }

    // US2-S16: a term that has already ended when the link is used makes it invalid.
    [Fact]
    public async Task ATermThatEndedBeforeAcceptance_IsInvalid()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Active);
        var inviteeId = await WithDbAsync(CreateUserAsync);
        var termEnd = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(1);
        var token = await InviteAsync(admin, community, CommunityRole.BoardMember, EmailOf(inviteeId), termEnd.ToString("yyyy-MM-dd"));
        Clock.UtcNow = Clock.UtcNow.AddHours(50);
        using var invitee = await ClientForAsync(EmailOf(inviteeId));

        var res = await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await MembershipAsync(community, inviteeId, CommunityRole.BoardMember));
    }

    // R7: an end-dated manager invitation is re-checked against the open-ended rule at acceptance.
    [Fact]
    public async Task AnEndDatedManagerInvitation_IsRefusedAtAcceptance_WhenNoOpenEndedManagerRemains()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var inviteeId = await WithDbAsync(CreateUserAsync);
        using var manager = await ClientForAsync(managerEmail);
        var token = await InviteAsync(manager, community, CommunityRole.CommunityManager, EmailOf(inviteeId), "2027-06-30");
        // The open-ended manager gets an end date directly, so no rule ran in between.
        await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == managerId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.EndDate, new DateOnly(2027, 6, 30))));
        using var invitee = await ClientForAsync(EmailOf(inviteeId));

        var res = await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("LAST_MANAGER", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await MembershipAsync(community, inviteeId, CommunityRole.CommunityManager));
        Assert.Equal(InvitationState.Pending,
            await WithDbAsync(db => db.Invitations.Where(i => i.CommunityId == community).Select(i => i.State).SingleAsync()));
    }

    // FR-022: accepting a role you already hold marks the invitation accepted and adds nothing.
    [Fact]
    public async Task AcceptingWhileAlreadyHoldingTheRole_AddsNothing()
    {
        var (community, admin, _) = await CommunityWithAdminAsync(CommunityStatus.Active);
        var inviteeId = await WithDbAsync(CreateUserAsync);
        var token = await InviteAsync(admin, community, CommunityRole.Accountant, EmailOf(inviteeId));
        await WithDbAsync(db => AddMembershipAsync(db, inviteeId, community, CommunityRole.Accountant));
        using var invitee = await ClientForAsync(EmailOf(inviteeId));

        var res = await invitee.PostAsJsonAsync("/api/v1/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await JsonAsync(res)).GetProperty("alreadyHeld").GetBoolean());
        Assert.Equal(1, await WithDbAsync(db => db.CommunityMemberships.CountAsync(m => m.CommunityId == community && m.UserId == inviteeId)));
        Assert.Equal(InvitationState.Accepted,
            await WithDbAsync(db => db.Invitations.Where(i => i.CommunityId == community).Select(i => i.State).SingleAsync()));
    }

    private async Task<(Guid Community, HttpClient Admin, Guid CompanyId)> CommunityWithAdminAsync(CommunityStatus status)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, status);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        return (community, await ClientForAsync(adminEmail), companyId);
    }

    private async Task<string> InviteAsync(HttpClient sender, Guid community, CommunityRole role, string email, string? endDate = null)
    {
        var res = await sender.PostAsJsonAsync($"/api/v1/communities/{community}/memberships", new { role = (int)role, email, endDate });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return TokensSentTo(email).Last();
    }

    private List<string> TokensSentTo(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => LinkToken().Match(m.Body).Groups[1].Value).ToList();

    private Task<CommunityMembership?> MembershipAsync(Guid community, string userId, CommunityRole role) =>
        WithDbAsync(db => db.CommunityMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.CommunityId == community && m.UserId == userId && m.Role == role));
}

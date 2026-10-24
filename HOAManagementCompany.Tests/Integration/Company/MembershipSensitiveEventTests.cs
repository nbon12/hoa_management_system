using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T082 — US2-S19: every membership and invitation change is one sensitive event with the
/// actor, community, target and UTC time, and no event renders an email, a name or a token
/// (FR-023, FR-040).
/// </summary>
public partial class MembershipSensitiveEventTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    // US2-S19
    [Fact]
    public async Task EachChange_LogsExactlyOneEvent_WithoutEmailsNamesOrTokens()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var (leeId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (samId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (kimId, _) = await CreateStaffAsync(community, CommunityRole.Accountant);
        using var manager = await ClientForAsync(managerEmail);
        using var admin = await ClientForAsync(adminEmail);

        var t0 = Clock.UtcNow;

        // Appoint directly, end-date, end, downgrade and replace.
        var leeManager = (await JsonAsync(await manager.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.CommunityManager, userId = leeId }))).GetProperty("id").GetGuid();
        var kimRow = await RowAsync(community, kimId, CommunityRole.Accountant);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync($"{Url(community)}/{kimRow}", new { endDate = "2027-12-31" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync($"{Url(community)}/{kimRow}", new { end = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync($"{Url(community)}/{leeManager}",
            new { downgradeTo = (int)CommunityRole.BoardMember })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/communities/{community}/managers/replace",
            new { currentManagerUserId = managerId, replacementUserId = samId })).StatusCode);

        // Invite, resend, revoke, a failed accept and an accept.
        using var sam = await ClientForAsync(EmailOf(samId));
        var invitee = await WithDbAsync(CreateUserAsync);
        var bob = await WithDbAsync(CreateUserAsync);
        var invitationId = (await JsonAsync(await sam.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.BoardMember, email = EmailOf(invitee) }))).GetProperty("invitationId").GetGuid();
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        var t1 = Clock.UtcNow;
        Assert.Equal(HttpStatusCode.OK, (await sam.PostAsync($"/api/v1/communities/{community}/invitations/{invitationId}/resend", null)).StatusCode);
        var token = Tokens(EmailOf(invitee)).Last();
        using var bobClient = await ClientForAsync(EmailOf(bob));
        Assert.Equal(HttpStatusCode.Forbidden, (await bobClient.PostAsJsonAsync("/api/v1/invitations/accept", new { token })).StatusCode);
        using var inviteeClient = await ClientForAsync(EmailOf(invitee));
        Assert.Equal(HttpStatusCode.OK, (await inviteeClient.PostAsJsonAsync("/api/v1/invitations/accept", new { token })).StatusCode);
        var revokedId = (await JsonAsync(await sam.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.Accountant, email = $"rev-{Guid.NewGuid():N}@example.com" }))).GetProperty("invitationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await sam.PostAsync($"/api/v1/communities/{community}/invitations/{revokedId}/revoke", null)).StatusCode);

        var memberships = Events("BoardMembershipChange", community);
        AssertOne(memberships, leeId, "grant", managerId);
        AssertOne(memberships, kimId, "edit", managerId);
        AssertOne(memberships, kimId, "end", managerId);
        AssertOne(memberships, leeId, "downgrade", managerId);
        AssertOne(memberships, samId, "replace-grant", adminId);
        AssertOne(memberships, managerId, "replace-end", adminId);
        // The acceptance's grant is its own event, caused by the invitation.
        var accepted = Assert.Single(memberships, e => Prop(e, "TargetUserId") == invitee && Prop(e, "Kind") == "grant");
        Assert.Equal((invitee, $"invitation:{invitationId}", t1), (Prop(accepted, "ActorId"), Prop(accepted, "Cause"), At(accepted)));
        Assert.All(memberships.Where(e => e != accepted), e => Assert.Equal(t0, At(e)));

        var invitations = Events("InvitationChange", community).Where(e => Prop(e, "InvitationId") == invitationId.ToString()).ToList();
        Assert.Equal(["Created", "Resent", "Accepted"], invitations.Select(e => Prop(e, "Change")));
        Assert.Equal([samId, samId, invitee], invitations.Select(e => Prop(e, "ActorId")));
        Assert.Equal([t0, t1, t1], invitations.Select(At));
        var revoked = Assert.Single(Events("InvitationChange", community), e => Prop(e, "InvitationId") == revokedId.ToString() && Prop(e, "Change") == "Revoked");
        Assert.Equal(("Manual", samId, t1), (Prop(revoked, "Reason"), Prop(revoked, "ActorId"), At(revoked)));
        var failed = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("InvitationAcceptFailed")
                                                        && Prop(e, "InvitationId") == invitationId.ToString());
        Assert.Equal(("INVITATION_EMAIL_MISMATCH", bob, community.ToString(), t1),
            (Prop(failed, "Reason"), Prop(failed, "UserId"), Prop(failed, "CommunityId"), At(failed)));

        var rendered = string.Join("\n", memberships.Concat(Events("InvitationChange", community)).Append(failed)
            .Select(e => e.RenderMessage() + " " + string.Join(" ", e.Properties.Values)));
        foreach (var secret in Tokens(EmailOf(invitee)).Concat([EmailOf(invitee), EmailOf(bob), managerEmail, "Board Tester", "@"]))
            Assert.DoesNotContain(secret, rendered, StringComparison.OrdinalIgnoreCase);
    }

    private List<LogEvent> Events(string template, Guid community) => LogSink.Events
        .Where(e => e.MessageTemplate.Text.StartsWith(template) && Prop(e, "CommunityId") == community.ToString())
        .ToList();

    private static void AssertOne(List<LogEvent> events, string target, string kind, string actor)
    {
        var e = Assert.Single(events, x => Prop(x, "TargetUserId") == target && Prop(x, "Kind") == kind);
        Assert.Equal(actor, Prop(e, "ActorId"));
    }

    private static DateTimeOffset At(LogEvent e) => (DateTimeOffset)((ScalarValue)e.Properties["UtcNow"]).Value!;

    private static string? Prop(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    private Task<Guid> RowAsync(Guid community, string userId, CommunityRole role) => WithDbAsync(db =>
        db.CommunityMemberships.Where(m => m.CommunityId == community && m.UserId == userId && m.Role == role)
            .Select(m => m.Id).SingleAsync());

    private List<string> Tokens(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => LinkToken().Match(m.Body).Groups[1].Value).ToList();

    private static string Url(Guid community) => $"/api/v1/communities/{community}/memberships";
}

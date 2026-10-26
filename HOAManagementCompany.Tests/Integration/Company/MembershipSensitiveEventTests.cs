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
        var invitee = await WithDbAsync(CreateUserAsync);
        var bob = await WithDbAsync(CreateUserAsync);
        // Names no template, ID or logged value contains, so a first or last name leaked on its own is caught.
        (string UserId, string First, string Last)[] people =
        [
            (adminId, "Aurelio", "Quackenbush"), (managerId, "Perpetua", "Wolstenholme"), (leeId, "Leocadio", "Vandermeer"),
            (samId, "Samwise", "Fitzwilliam"), (kimId, "Kimberlyn", "Xanthopoulos"), (invitee, "Ignatius", "Zuckerkandl"),
            (bob, "Bartholomew", "Quillfeather"),
        ];
        foreach (var (userId, first, last) in people)
            await RenameAsync(userId, first, last);
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

        // Invite an account holder, resend, a failed accept and an accept; invite an address with no account and revoke.
        using var sam = await ClientForAsync(EmailOf(samId));
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
        var revEmail = $"rev-{Guid.NewGuid():N}@example.com";
        var revokedId = (await JsonAsync(await sam.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.Accountant, email = revEmail }))).GetProperty("invitationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await sam.PostAsync($"/api/v1/communities/{community}/invitations/{revokedId}/revoke", null)).StatusCode);

        // Exactly one event per change: no change logged twice or under a second kind.
        var memberships = Events("BoardMembershipChange", community);
        Assert.Equal(
            [(leeId, "grant", managerId), (kimId, "edit", managerId), (kimId, "end", managerId), (leeId, "downgrade", managerId),
                (samId, "replace-grant", adminId), (managerId, "replace-end", adminId), (invitee, "grant", invitee)],
            memberships.Select(e => (Prop(e, "TargetUserId"), Prop(e, "Kind"), Prop(e, "ActorId"))));
        // The acceptance's grant is its own event, caused by the invitation.
        var accepted = memberships[^1];
        Assert.Equal(($"invitation:{invitationId}", t1), (Prop(accepted, "Cause"), At(accepted)));
        Assert.All(memberships.SkipLast(1), e => Assert.Equal(t0, At(e)));

        // FR-023: the account holder's invitation names them by user ID; the no-account one by invitation ID alone.
        var invitations = Events("InvitationChange", community);
        Assert.Equal(
            [(invitationId.ToString(), "Created", samId, invitee, t0), (invitationId.ToString(), "Resent", samId, invitee, t1),
                (invitationId.ToString(), "Accepted", invitee, invitee, t1),
                (revokedId.ToString(), "Created", samId, "null", t1), (revokedId.ToString(), "Revoked", samId, "null", t1)],
            invitations.Select(e => (Prop(e, "InvitationId"), Prop(e, "Change"), Prop(e, "ActorId"), Prop(e, "TargetUserId"), At(e))));
        Assert.Equal("Manual", Prop(invitations[^1], "Reason"));
        var failed = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("InvitationAcceptFailed")
                                                        && Prop(e, "InvitationId") == invitationId.ToString());
        Assert.Equal(("INVITATION_EMAIL_MISMATCH", bob, community.ToString(), t1),
            (Prop(failed, "Reason"), Prop(failed, "UserId"), Prop(failed, "CommunityId"), At(failed)));

        var rendered = string.Join("\n", memberships.Concat(invitations).Append(failed)
            .Select(e => e.RenderMessage() + " " + string.Join(" ", e.Properties.Values)));
        var secrets = Tokens(EmailOf(invitee)).Concat(Tokens(revEmail))
            .Concat([EmailOf(invitee), EmailOf(bob), managerEmail, revEmail, "@"])
            .Concat(people.SelectMany(p => new[] { p.First, p.Last }));
        foreach (var secret in secrets)
            Assert.DoesNotContain(secret, rendered, StringComparison.OrdinalIgnoreCase);
    }

    private List<LogEvent> Events(string template, Guid community) => LogSink.Events
        .Where(e => e.MessageTemplate.Text.StartsWith(template) && Prop(e, "CommunityId") == community.ToString())
        .ToList();

    private static DateTimeOffset At(LogEvent e) => (DateTimeOffset)((ScalarValue)e.Properties["UtcNow"]).Value!;

    private static string? Prop(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    private Task<Guid> RowAsync(Guid community, string userId, CommunityRole role) => WithDbAsync(db =>
        db.CommunityMemberships.Where(m => m.CommunityId == community && m.UserId == userId && m.Role == role)
            .Select(m => m.Id).SingleAsync());

    private Task RenameAsync(string userId, string firstName, string lastName) => WithDbAsync(async db =>
        Assert.Equal(1, await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.FirstName, firstName).SetProperty(u => u.LastName, lastName))));

    private List<string> Tokens(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => LinkToken().Match(m.Body).Groups[1].Value).ToList();

    private static string Url(Guid community) => $"/api/v1/communities/{community}/memberships";
}

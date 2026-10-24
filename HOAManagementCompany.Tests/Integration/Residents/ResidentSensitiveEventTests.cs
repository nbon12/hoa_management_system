using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T114 — US8-S19: every residency invitation and resident-link change is one sensitive event
/// with the actor, community, property, target and UTC time, and no event renders an email, a
/// name, a street address or a token (FR-024c, FR-040).
/// </summary>
public partial class ResidentSensitiveEventTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Address = "711 Keystone Park Dr #29";

    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    // US8-S19
    [Fact]
    public async Task EachChange_LogsExactlyOneEvent_WithoutEmailsNamesAddressesOrTokens()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community, Address);
        using var manager = await ClientForAsync(managerEmail);
        var janeId = await WithDbAsync(CreateUserAsync);
        var bobId = await WithDbAsync(CreateUserAsync);

        var t0 = Clock.UtcNow;

        // Invite, resend, a failed accept (wrong account), an accept, then a removal.
        var invitationId = (await JsonAsync(await InviteAsync(manager, community, home, EmailOf(janeId))))
            .GetProperty("invitationId").GetGuid();
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        var t1 = Clock.UtcNow;
        Assert.Equal(HttpStatusCode.OK,
            (await manager.PostAsync($"/api/v1/communities/{community}/invitations/{invitationId}/resend", null)).StatusCode);
        var token = Tokens(EmailOf(janeId)).Last();
        using (var bob = await ClientForAsync(EmailOf(bobId)))
            Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/invitations/accept", new { token })).StatusCode);
        using (var jane = await ClientForAsync(EmailOf(janeId)))
            Assert.Equal(HttpStatusCode.OK, (await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await manager.DeleteAsync($"/api/v1/communities/{community}/residents/{home}/{janeId}")).StatusCode);

        // And a revoke of a second invitation.
        var revokedEmail = $"kim-{Guid.NewGuid():N}@example.com";
        var revokedId = (await JsonAsync(await InviteAsync(manager, community, home, revokedEmail)))
            .GetProperty("invitationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await manager.PostAsync($"/api/v1/communities/{community}/invitations/{revokedId}/revoke", null)).StatusCode);

        var invitations = Events("InvitationChange", community).Where(e => Prop(e, "InvitationId") == invitationId.ToString()).ToList();
        Assert.Equal(["Created", "Resent", "Accepted"], invitations.Select(e => Prop(e, "Change")));
        Assert.Equal([managerId, managerId, janeId], invitations.Select(e => Prop(e, "ActorId")));
        Assert.All(invitations, e => Assert.Equal((home.ToString(), "Residency"), (Prop(e, "PropertyId"), Prop(e, "Kind"))));
        var revoked = Assert.Single(Events("InvitationChange", community),
            e => Prop(e, "InvitationId") == revokedId.ToString() && Prop(e, "Change") == "Revoked");
        Assert.Equal(("Manual", managerId, home.ToString()), (Prop(revoked, "Reason"), Prop(revoked, "ActorId"), Prop(revoked, "PropertyId")));

        var failed = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("InvitationAcceptFailed")
                                                        && Prop(e, "InvitationId") == invitationId.ToString());
        Assert.Equal(("INVITATION_EMAIL_MISMATCH", bobId, home.ToString(), community.ToString()),
            (Prop(failed, "Reason"), Prop(failed, "UserId"), Prop(failed, "PropertyId"), Prop(failed, "CommunityId")));

        var links = Events("ResidentLinkChange", community);
        Assert.Equal([("Linked", janeId), ("Removed", managerId)], links.Select(e => (Prop(e, "Change"), Prop(e, "ActorId"))));
        Assert.All(links, e => Assert.Equal((home.ToString(), janeId), (Prop(e, "PropertyId"), Prop(e, "TargetUserId"))));

        var all = invitations.Append(revoked).Append(failed).Concat(links).ToList();
        // UTC times: the invite before the clock moved, everything after it at the new time.
        Assert.Equal(t0, At(invitations[0]));
        Assert.All(all.Skip(1), e => Assert.Equal(t1, At(e)));
        var rendered = string.Join("\n", all.Select(e => e.RenderMessage() + " " + string.Join(" ", e.Properties.Values)));
        foreach (var secret in Tokens(EmailOf(janeId)).Concat([EmailOf(janeId), EmailOf(bobId), revokedEmail, managerEmail,
                     "Board Tester", "Keystone Park", "@"]))
            Assert.DoesNotContain(secret, rendered, StringComparison.OrdinalIgnoreCase);
    }

    // FR-024e: a sign-up from the link that is refused is a failed acceptance, and is logged.
    [Fact]
    public async Task ARefusedSignUpFromTheLink_IsLoggedAsAFailedAcceptance()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community, Address);
        using var manager = await ClientForAsync(managerEmail);
        var jane = $"jane-{Guid.NewGuid():N}@example.com";
        var invitationId = (await JsonAsync(await InviteAsync(manager, community, home, jane))).GetProperty("invitationId").GetGuid();

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token = Tokens(jane).Single(), firstName = "Jane", lastName = "Alvarez", password = "short" });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var failed = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("InvitationAcceptFailed")
                                                        && Prop(e, "InvitationId") == invitationId.ToString());
        Assert.Equal(("VALIDATION_ERROR", home.ToString()), (Prop(failed, "Reason"), Prop(failed, "PropertyId")));
        Assert.DoesNotContain(jane, failed.RenderMessage() + string.Join(" ", failed.Properties.Values));
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient sender, Guid community, Guid propertyId, string email) =>
        sender.PostAsJsonAsync($"/api/v1/communities/{community}/residents/invitations", new { propertyId, email });

    private List<LogEvent> Events(string template, Guid community) => LogSink.Events
        .Where(e => e.MessageTemplate.Text.StartsWith(template) && Prop(e, "CommunityId") == community.ToString())
        .ToList();

    private static DateTimeOffset At(LogEvent e) => (DateTimeOffset)((ScalarValue)e.Properties["UtcNow"]).Value!;

    private static string? Prop(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    private List<string> Tokens(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => LinkToken().Match(m.Body))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
}

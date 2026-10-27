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
/// 030 T152 — FR-040, SC-007 and spec Observability. One of each of the feature's actions runs,
/// then the captured log events and exported spans are checked: each caused change has its own
/// event carrying the parent's actor and cause, nothing is logged twice, request spans carry the
/// community and capability tags, and no email, name, street address or token appears anywhere.
/// Every client uses a minted token, because the pre-existing sign-in log records the email.
/// </summary>
public partial class CompanyTelemetryHygieneTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Street = "4417 Sentinel Crest Ln";

    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    [Fact]
    public async Task TheFeaturesActions_LogEachChangeOnce_AndLeaveNoPersonalDataInTelemetry()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var (secondAdminId, _) = await CreateCompanyAdminAsync(companyId);
        using var admin = ClientWithToken(MintToken(adminId));
        using var secondAdmin = ClientWithToken(MintToken(secondAdminId));

        // Add a community, staff it, and change its profile.
        var created = await admin.PostAsJsonAsync("/api/v1/communities", new
        {
            legalName = "Sentinel Crest HOA, Inc.", communityName = $"Sentinel Crest {Guid.NewGuid():N}"[..23], county = "Wake",
            formationDate = "2004-03-14", managementStartDate = "2026-11-01",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var community = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var (managerId, _) = await CreateManagerAsync(community);
        using var manager = ClientWithToken(MintToken(managerId));
        var (leeId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync($"/api/v1/communities/{community}/profile", new { county = "Durham" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.CommunityManager, userId = leeId })).StatusCode);

        // A membership invitation: sent, resent, accepted.
        var patId = await WithDbAsync(CreateUserAsync);
        var membershipInvite = (await JsonAsync(await manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.Accountant, email = EmailOf(patId) }))).GetProperty("invitationId").GetGuid();
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/api/v1/communities/{community}/invitations/{membershipInvite}/resend", null)).StatusCode);
        using (var pat = ClientWithToken(MintToken(patId)))
            Assert.Equal(HttpStatusCode.OK, (await pat.PostAsJsonAsync("/api/v1/invitations/accept", new { token = Tokens(EmailOf(patId)).Last() })).StatusCode);

        // Mark it Active, so a residency invitation is sent rather than scheduled.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/activate", null)).StatusCode);

        // A resident joins by invitation, enrolls nothing, and an autopay with no recorded enroller is cancelled on removal.
        var home = await CreatePropertyAsync(community, Street);
        var janeId = await WithDbAsync(CreateUserAsync);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync($"/api/v1/communities/{community}/residents/invitations",
            new { propertyId = home, email = EmailOf(janeId) })).StatusCode);
        using (var jane = ClientWithToken(MintToken(janeId)))
            Assert.Equal(HttpStatusCode.OK, (await jane.PostAsJsonAsync("/api/v1/invitations/accept", new { token = Tokens(EmailOf(janeId)).Last() })).StatusCode);
        var removedSchedule = await CreateAutopayAsync(home, null);
        Assert.Equal(HttpStatusCode.OK, (await manager.DeleteAsync($"/api/v1/communities/{community}/residents/{home}/{janeId}")).StatusCode);

        // A Company Administrator invitation the second administrator sent, orphaned when they are revoked.
        var adminInvite = (await JsonAsync(await secondAdmin.PostAsJsonAsync("/api/v1/company/administrators/invitations",
            new { email = $"ops-{Guid.NewGuid():N}@example.com" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/company/administrators/{secondAdminId}/revoke", null)).StatusCode);

        // Replace the managers, then archive (with a pending invitation and an autopay) and restore.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/communities/{community}/managers/replace",
            new { currentManagerUserId = leeId, replacementUserId = patId })).StatusCode);
        var kimInvite = (await JsonAsync(await manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.BoardMember, email = $"kim-{Guid.NewGuid():N}@example.com" }))).GetProperty("invitationId").GetGuid();
        var otherHome = await CreatePropertyAsync(community);
        await CreateResidentAsync(otherHome);
        var archivedSchedule = await CreateAutopayAsync(otherHome, null);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/communities/{community}/restore", null)).StatusCode);
        FlushTelemetry();

        // Each caused change has its own event, with the parent's actor and cause.
        var kimRevoked = Assert.Single(Events("InvitationChange"), e => Prop(e, "InvitationId") == kimInvite.ToString() && Prop(e, "Change") == "Revoked");
        Assert.Equal((adminId, "CommunityArchived", "CommunityArchived"), (Prop(kimRevoked, "ActorId"), Prop(kimRevoked, "Reason"), Prop(kimRevoked, "Cause")));
        var orphan = Assert.Single(Events("InvitationChange"), e => Prop(e, "InvitationId") == adminInvite.ToString() && Prop(e, "Change") == "Revoked");
        Assert.Equal((adminId, "SenderLostAuthority", "CompanyAdminRevoked"), (Prop(orphan, "ActorId"), Prop(orphan, "Reason"), Prop(orphan, "Cause")));
        var removalCancel = Assert.Single(Events("AutopayCancelled"), e => Prop(e, "RecurringId") == removedSchedule.ToString());
        Assert.Equal((managerId, "ResidentRemoved"), (Prop(removalCancel, "ActorId"), Prop(removalCancel, "Cause")));
        var archiveCancel = Assert.Single(Events("AutopayCancelled"), e => Prop(e, "RecurringId") == archivedSchedule.ToString());
        Assert.Equal((adminId, "CommunityArchived"), (Prop(archiveCancel, "ActorId"), Prop(archiveCancel, "Cause")));
        var replace = Events("BoardMembershipChange").Where(e => Prop(e, "Kind") is "replace-grant" or "replace-end").ToList();
        Assert.Equal(2, replace.Count);
        Assert.All(replace, e => Assert.Equal(adminId, Prop(e, "ActorId")));
        Assert.Single(replace.Select(e => Prop(e, "Cause")).Distinct());
        Assert.StartsWith("replace:", Prop(replace[0], "Cause"));

        // Nothing is logged twice.
        foreach (var group in Events("InvitationChange").GroupBy(e => (Prop(e, "InvitationId"), Prop(e, "Change"), Prop(e, "UtcNow"))))
            Assert.Single(group);
        foreach (var group in Events("BoardMembershipChange").GroupBy(e => (Prop(e, "MembershipId"), Prop(e, "Kind"), Prop(e, "UtcNow"))))
            Assert.Single(group);
        foreach (var group in Events("CommunityLifecycle").GroupBy(e => (Prop(e, "CommunityId"), Prop(e, "Action"))))
            Assert.Single(group);
        foreach (var group in Events("ResidentLinkChange").GroupBy(e => (Prop(e, "PropertyId"), Prop(e, "TargetUserId"), Prop(e, "Change"))))
            Assert.Single(group);

        // Request spans carry the tags: board and invitation routes the community and capability,
        // company routes the capability.
        Assert.Contains(ExportedSpans, s => Tag(s, "community.id") == community.ToString() && Tag(s, "capability") == "EditCommunityProfile");
        Assert.Contains(ExportedSpans, s => Tag(s, "community.id") == community.ToString() && Tag(s, "capability") == "ManageResidents");
        Assert.Contains(ExportedSpans, s => Tag(s, "capability") == "ManageCompanyAdministrators");
        Assert.Contains(ExportedSpans, s => Tag(s, "capability") == "CreateCommunity");
        Assert.All(ExportedSpans.Where(s => Tag(s, "capability") is not null), s =>
        {
            Assert.DoesNotContain("@", Tag(s, "capability"));
            Assert.Matches("^[0-9a-f-]{36}$", Tag(s, "community.id") ?? Guid.Empty.ToString());
        });

        // No email, name, street address or token anywhere.
        var emails = new[] { adminId, secondAdminId, managerId, leeId, patId, janeId }.Select(EmailOf).ToList();
        var secrets = emails.Concat(Email.Sent.Select(m => m.Target)).Concat(Email.Sent.SelectMany(m => Tokens(m)))
            .Concat(["Board Tester", "Pat Owner", Street, "Sentinel Crest Ln"]).Distinct().ToList();
        var logText = string.Join("\n", LogSink.Events.Select(e => e.RenderMessage() + " " + string.Join(" ", e.Properties.Values)));
        var spanText = string.Join("\n", ExportedSpans.SelectMany(s => s.TagObjects.Select(t => $"{t.Key}={t.Value}")));
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, logText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, spanText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private List<LogEvent> Events(string template) =>
        LogSink.Events.Where(e => e.MessageTemplate.Text.StartsWith(template + ":")).ToList();

    private static string? Prop(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    private static string? Tag(System.Diagnostics.Activity span, string name) => span.GetTagItem(name)?.ToString();

    private List<string> Tokens(string email) =>
        Email.Sent.Where(m => m.Target == email).SelectMany(m => Tokens(m)).ToList();

    private static IEnumerable<string> Tokens(HOAManagementCompany.Infrastructure.Payments.Alerts.AlertMessage m) =>
        LinkToken().Matches(m.Body).Select(x => x.Groups[1].Value);
}

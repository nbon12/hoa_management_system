using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Features.Payments.Recurring;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T094 — archiving a community (US3; FR-012–FR-014; contracts/company.md § archive).
/// </summary>
public class LifecycleArchiveTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US3-S5
    [Fact]
    public async Task Archiving_HidesItFromMembers_RevokesOpenInvitations_AndKeepsEveryRecordReadable()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var (_, boardEmail) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var manager = await ClientForAsync(managerEmail);
        using var board = await ClientForAsync(boardEmail);
        using var admin = await ClientForAsync(adminEmail);
        var drewId = await WithDbAsync(CreateUserAsync);
        var managerInvite = (await JsonAsync(await manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.CommunityManager, email = EmailOf(drewId) }))).GetProperty("invitationId").GetGuid();
        var managerToken = System.Text.RegularExpressions.Regex.Match(Email.Sent.Last(m => m.Target == EmailOf(drewId)).Body,
            "/invite#t=([A-Za-z0-9_-]{43})").Groups[1].Value;
        var (residentInvite, residentToken) = await InsertResidencyInvitationAsync(companyId, community,
            await CreatePropertyAsync(community), $"kim-{Guid.NewGuid():N}@example.com", managerId, InvitationState.Pending);
        Assert.Contains(await MyCommunitiesAsync(manager), id => id == community);
        var before = await CountsAsync(community);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/archive", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("Archived", 2), (body.GetProperty("status").GetString(), body.GetProperty("invitationsRevoked").GetInt32()));
        Assert.Equal(CommunityStatus.Archived, await StatusAsync(community));
        Assert.DoesNotContain(await MyCommunitiesAsync(manager), id => id == community);
        Assert.DoesNotContain(await MyCommunitiesAsync(board), id => id == community);

        foreach (var id in new[] { managerInvite, residentInvite })
        {
            var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == id));
            Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.CommunityArchived),
                (invitation.State, invitation.RevokeReason));
        }
        using var drew = await ClientForAsync(EmailOf(drewId));
        foreach (var token in new[] { managerToken, residentToken! })
        {
            var accept = await drew.PostAsJsonAsync("/api/v1/invitations/accept", new { token });
            Assert.Equal(HttpStatusCode.Gone, accept.StatusCode);
            Assert.Equal("INVITATION_INVALID", (await JsonAsync(accept)).GetProperty("code").GetString());
        }

        foreach (var path in new[] { "settings", "profile", "memberships", "invitations" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/communities/{community}/{path}")).StatusCode);
        Assert.Equal(before, await CountsAsync(community));
    }

    // US3-S7
    [Fact]
    public async Task Archiving_CancelsEveryAutopay_AndEmailsEachResidentOnce()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, name: $"Old Mill Commons {Guid.NewGuid():N}"[..30]);
        var a = await CreatePropertyAsync(community, "16 Old Mill Ln");
        var b = await CreatePropertyAsync(community, "18 Old Mill Ln");
        var c = await CreatePropertyAsync(community, "20 Old Mill Ln");
        var (samId, _) = await CreateResidentAsync(a);
        await LinkResidentAsync(samId, b);
        var (janeId, _) = await CreateResidentAsync(c);
        var (lukeId, _) = await CreateResidentAsync(await CreatePropertyAsync(community, "22 Old Mill Ln"));
        var schedules = new[] { await CreateAutopayAsync(a, samId), await CreateAutopayAsync(b, samId), await CreateAutopayAsync(c, janeId) };
        await WithDbAsync(db => db.RecurringPayments.Where(r => schedules.Contains(r.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DraftDay, DateTime.UtcNow.Day)));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var body = await JsonAsync(await admin.PostAsync($"/api/v1/communities/{community}/archive", null));

        Assert.Equal((3, 3), (body.GetProperty("autopayCancelled").GetInt32(), body.GetProperty("residentsEmailed").GetInt32()));
        await WithDbAsync(async db =>
        {
            var rows = await db.RecurringPayments.AsNoTracking().Include(r => r.Authorizations)
                .Where(r => schedules.Contains(r.Id)).ToListAsync();
            Assert.All(rows, r =>
            {
                Assert.Equal(("inactive", RecurringCancellationReasons.CommunityArchived), (r.Status, r.CancellationReason));
                Assert.NotNull(r.Authorizations.Single(x => x.Id == r.CurrentAuthorizationId).TerminatedAt);
            });
        });

        // No charge starts for them on their draft day.
        using (var scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<RecurringDraftService>()
                .RunDueDraftsAsync(DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Empty(Stripe.OffSessionCharges);
        Assert.False(await WithDbAsync(db => db.PaymentTransactions.AnyAsync(t => t.PropertyId == a || t.PropertyId == b || t.PropertyId == c)));

        var emails = await WithDbAsync(db => db.OutboxMessages.AsNoTracking()
            .Where(m => m.Kind == CompanyEmailKinds.CommunityArchived && m.DedupKey!.Contains(community.ToString())).ToListAsync());
        Assert.Equal(new[] { samId, janeId, lukeId }.Order(), emails.Select(m => m.RecipientUserId!).Order());
        var sam = JsonSerializer.Deserialize<AlertMessage>(emails.Single(m => m.RecipientUserId == samId).PayloadJson)!;
        Assert.Contains("16 Old Mill Ln", sam.Body);
        Assert.Contains("18 Old Mill Ln", sam.Body);
        Assert.Contains("Autopay for 16 Old Mill Ln has ended.", sam.Body);
        Assert.Contains("Autopay for 18 Old Mill Ln has ended.", sam.Body);
        var luke = JsonSerializer.Deserialize<AlertMessage>(emails.Single(m => m.RecipientUserId == lukeId).PayloadJson)!;
        Assert.DoesNotContain("Autopay for", luke.Body);
        Assert.Equal(3, LogSink.Events.Count(e => e.MessageTemplate.Text.StartsWith("AutopayCancelled")
            && schedules.Any(s => e.Properties["RecurringId"].ToString() == s.ToString())));
    }

    // US3-S10
    [Fact]
    public async Task Archiving_WithOpenApplications_IsOpenWork_ListingEach_AndChangesNothing()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var home = await CreatePropertyAsync(community);
        await CreateArcApplicationAsync(community, home, 1041);
        await CreateArcApplicationAsync(community, home, 1042, ArcApplicationStatus.DecisionReached);
        await CreateArcApplicationAsync(community, home, 1040, ArcApplicationStatus.Closed);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/archive", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("OPEN_WORK", body.GetProperty("code").GetString());
        var items = body.GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("kind").GetString(), i.GetProperty("label").GetString(), i.GetProperty("status").GetString())).ToList();
        Assert.Equal([("ArcApplication", "ARC-1041", "Open"), ("ArcApplication", "ARC-1042", "Decision reached")], items);
        Assert.Contains("ARC-1041", body.GetProperty("message").GetString());
        Assert.Contains("ARC-1042", body.GetProperty("message").GetString());
        Assert.Equal(CommunityStatus.Active, await StatusAsync(community));
    }

    // US3-S11
    [Fact]
    public async Task ArchivingAMaster_WithALiveSubAssociation_IsOpenWork()
    {
        var companyId = await CreateCompanyAsync();
        var master = await CreateCommunityInAsync(companyId);
        var subName = $"Keystone Crossing SF {Guid.NewGuid():N}"[..30];
        await CreateCommunityInAsync(companyId, name: subName, parentId: master);
        await CreateCommunityInAsync(companyId, CommunityStatus.Archived, parentId: master);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/communities/{master}/archive", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var item = Assert.Single((await JsonAsync(res)).GetProperty("items").EnumerateArray());
        Assert.Equal(("SubAssociation", subName, "Active"),
            (item.GetProperty("kind").GetString(), item.GetProperty("label").GetString(), item.GetProperty("status").GetString()));
        Assert.Equal(CommunityStatus.Active, await StatusAsync(master));
    }

    // Spec Edge Cases, SC-012: an Onboarding community's scheduled invitations are revoked and never emailed.
    [Fact]
    public async Task ArchivingAnOnboardingCommunity_RevokesItsScheduledInvitations_WithoutEmail()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (managerId, _) = await CreateManagerAsync(community);
        var emails = new[] { $"lena-{Guid.NewGuid():N}@example.com", $"omar-{Guid.NewGuid():N}@example.com" };
        foreach (var email in emails)
            await InsertResidencyInvitationAsync(companyId, community, await CreatePropertyAsync(community), email, managerId,
                InvitationState.Scheduled);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/archive", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var states = await WithDbAsync(db => db.Invitations.AsNoTracking().Where(i => i.CommunityId == community)
            .Select(i => new { i.State, i.RevokeReason }).ToListAsync());
        Assert.Equal(2, states.Count);
        Assert.All(states, s => Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.CommunityArchived),
            (s.State, s.RevokeReason)));
        Assert.DoesNotContain(Email.Sent, m => emails.Contains(m.Target));
    }

    [Fact]
    public async Task ThePreview_GivesTheCountsAndLists_WithoutChangingAnything()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var (residentId, _) = await CreateResidentAsync(home);
        await CreateAutopayAsync(home, residentId);
        var (inviteId, _) = await InsertResidencyInvitationAsync(companyId, community, await CreatePropertyAsync(community),
            $"drew-{Guid.NewGuid():N}@example.com", managerId, InvitationState.Pending);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var before = await CountsAsync(community);

        var preview = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/lifecycle/archive/preview"));

        Assert.True(preview.GetProperty("allowed").GetBoolean());
        var consequences = preview.GetProperty("consequences");
        Assert.Equal((1, 1), (consequences.GetProperty("autopayCancelled").GetInt32(), consequences.GetProperty("residentsEmailed").GetInt32()));
        Assert.Equal(inviteId, Assert.Single(consequences.GetProperty("invitationsRevoked").EnumerateArray()).GetProperty("id").GetGuid());
        var kept = Assert.Single(consequences.GetProperty("membershipsKept").EnumerateArray());
        Assert.Equal((managerId, "CommunityManager"), (kept.GetProperty("userId").GetString(), kept.GetProperty("role").GetString()));
        Assert.Equal(CommunityStatus.Active, await StatusAsync(community));
        Assert.Equal(before, await CountsAsync(community));
        Assert.Equal("active", await WithDbAsync(db => db.RecurringPayments.Where(r => r.PropertyId == home).Select(r => r.Status).SingleAsync()));

        // US3-S20
        using var manager = await ClientForAsync(managerEmail);
        foreach (var res in new[] { await manager.PostAsync($"/api/v1/communities/{community}/archive", null),
                                     await manager.GetAsync($"/api/v1/communities/{community}/lifecycle/archive/preview") })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.Equal(CommunityStatus.Active, await StatusAsync(community));
    }

    private Task<CommunityStatus> StatusAsync(Guid community) =>
        WithDbAsync(db => db.Communities.Where(c => c.Id == community).Select(c => c.Status).SingleAsync());

    private static async Task<List<Guid>> MyCommunitiesAsync(HttpClient client) =>
        (await JsonAsync(await client.GetAsync("/api/v1/me/communities"))).GetProperty("items").EnumerateArray()
        .Select(i => i.GetProperty("id").GetGuid()).ToList();

    // Rows an archive must keep (FR-012: none of its records may be deleted).
    private Task<string> CountsAsync(Guid community) => WithDbAsync(async db => string.Join("|",
        await db.CommunityMemberships.CountAsync(m => m.CommunityId == community),
        await db.Invitations.CountAsync(i => i.CommunityId == community),
        await db.Properties.CountAsync(p => p.CommunityId == community),
        await db.UserProperties.CountAsync(up => up.Property.CommunityId == community),
        await db.RecurringPayments.CountAsync(r => r.Property.CommunityId == community),
        await db.CommunityArcSettings.CountAsync(s => s.CommunityId == community)));
}

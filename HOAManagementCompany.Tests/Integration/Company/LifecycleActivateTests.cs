using System.Net;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T092 — marking a community Active (US3; FR-010, FR-024b; contracts/company.md § activate).
/// </summary>
public class LifecycleActivateTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US3-S3
    [Fact]
    public async Task Activating_WithAnOpenEndedManager_GoesLive_AndSendsTheScheduledInvitations()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (managerId, _) = await CreateManagerAsync(community);
        var lenaHome = await CreatePropertyAsync(community, "2 Ridgeview Ct");
        var omarHome = await CreatePropertyAsync(community, "8 Ridgeview Ct");
        var lena = $"lena-{Guid.NewGuid():N}@example.com";
        var omar = $"omar-{Guid.NewGuid():N}@example.com";
        var (lenaInvite, _) = await InsertResidencyInvitationAsync(companyId, community, lenaHome, lena, managerId, InvitationState.Scheduled);
        var (omarInvite, _) = await InsertResidencyInvitationAsync(companyId, community, omarHome, omar, managerId, InvitationState.Scheduled);
        var (residentId, residentEmail) = await CreateResidentAsync(await CreatePropertyAsync(community));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        Assert.Empty((await MeAsync(residentEmail)).GetProperty("properties").EnumerateArray());
        Clock.UtcNow = Clock.UtcNow.AddMinutes(10);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/activate", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("Active", 2, 0, 0), (body.GetProperty("status").GetString(), body.GetProperty("invitationsSent").GetInt32(),
            body.GetProperty("invitationsFailedToSend").GetInt32(), body.GetProperty("invitationsRevoked").GetInt32()));
        var row = await WithDbAsync(db => db.Communities.AsNoTracking().SingleAsync(c => c.Id == community));
        Assert.Equal((CommunityStatus.Active, (DateTimeOffset?)Clock.UtcNow), (row.Status, row.ActivatedAt));
        foreach (var (id, email) in new[] { (lenaInvite, lena), (omarInvite, omar) })
        {
            var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == id));
            Assert.Equal((InvitationState.Pending, (DateTimeOffset?)Clock.UtcNow, (DateTimeOffset?)Clock.UtcNow.AddHours(72)),
                (invitation.State, invitation.SentAt, invitation.ExpiresAt));
            Assert.Single(Email.Sent, m => m.Target == email);
        }
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityLifecycle")
            && e.Properties["CommunityId"].ToString() == community.ToString() && e.Properties["Action"].ToString() == "\"Activated\"");

        // The linked resident's next request lists the home.
        var me = await MeAsync(residentEmail);
        Assert.Contains(me.GetProperty("properties").EnumerateArray(), p => p.GetProperty("communityId").GetGuid() == community);
    }

    // US3-S3: a scheduled invitation whose sender lost authority is revoked instead of sent.
    [Fact]
    public async Task Activating_RevokesAScheduledInvitationWhoseSenderLostAuthority()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        await CreateManagerAsync(community);
        var (formerId, _) = await CreateManagerAsync(community);
        var email = $"kim-{Guid.NewGuid():N}@example.com";
        var (inviteId, _) = await InsertResidencyInvitationAsync(companyId, community, await CreatePropertyAsync(community), email,
            formerId, InvitationState.Scheduled);
        await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == formerId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Inactive)));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var body = await JsonAsync(await admin.PostAsync($"/api/v1/communities/{community}/activate", null));

        Assert.Equal((0, 1), (body.GetProperty("invitationsSent").GetInt32(), body.GetProperty("invitationsRevoked").GetInt32()));
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == inviteId));
        Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.SenderLostAuthority),
            (invitation.State, invitation.RevokeReason));
        Assert.DoesNotContain(Email.Sent, m => m.Target == email);
    }

    // US3-S4
    [Fact]
    public async Task Activating_WithoutAnOpenEndedManager_IsRefused_AndThePreviewAndSettingsSaySo()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        await CreateManagerAsync(community, endDate: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/activate", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("NO_ACTIVE_MANAGER", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(CommunityStatus.Onboarding, await StatusAsync(community));

        var preview = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/lifecycle/activate/preview"));
        Assert.False(preview.GetProperty("allowed").GetBoolean());
        Assert.Equal("NO_ACTIVE_MANAGER", preview.GetProperty("refusal").GetProperty("code").GetString());
        var settings = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/settings"));
        Assert.Equal("NO_ACTIVE_MANAGER", settings.GetProperty("can").GetProperty("activate").GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task Activating_AnActiveCommunity_IsAnInvalidTransition()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/communities/{community}/activate", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("INVALID_TRANSITION", (await JsonAsync(res)).GetProperty("code").GetString());
    }

    // US3-S20: status actions are Company Administrator only.
    [Fact]
    public async Task ACommunityManager_CantActivate()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PostAsync($"/api/v1/communities/{community}/activate", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(CommunityStatus.Onboarding, await StatusAsync(community));
    }

    private Task<CommunityStatus> StatusAsync(Guid community) =>
        WithDbAsync(db => db.Communities.Where(c => c.Id == community).Select(c => c.Status).SingleAsync());

    private async Task<System.Text.Json.JsonElement> MeAsync(string email)
    {
        using var client = await ClientForAsync(email);
        return await JsonAsync(await client.GetAsync("/api/v1/auth/me"));
    }
}

using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T078 — ending, end-dating and downgrading managers under the open-ended-manager rule
/// (US2; FR-019, FR-020; contracts/community-settings.md § PATCH memberships).
/// </summary>
public class ManagerEndAndDowngradeTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US2-S9
    [Fact]
    public async Task EndingTheOnlyOpenEndedManager_IsLastManager_AndTheManagerStaysActive()
    {
        var (community, managerId, managerRow, adminEmail) = await OneManagerAsync();
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PatchAsJsonAsync(MembershipUrl(community, managerRow), new { end = true });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("LAST_MANAGER", body.GetProperty("code").GetString());
        Assert.Contains("is the only Community Manager of", body.GetProperty("message").GetString());
        var row = await RowAsync(managerRow);
        Assert.Equal((MembershipStatus.Active, (DateOnly?)null), (row.Status, row.EndDate));
    }

    // US2-S10
    [Fact]
    public async Task GivingTheOnlyOpenEndedManagerAnEndDate_IsLastManager_AndTheEndDateStaysNull()
    {
        var (community, _, managerRow, adminEmail) = await OneManagerAsync();
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PatchAsJsonAsync(MembershipUrl(community, managerRow), new { endDate = "2026-12-31" });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("LAST_MANAGER", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null((await RowAsync(managerRow)).EndDate);
    }

    // US2-S12
    [Fact]
    public async Task AManagerEndsTheirOwnMembership_WhileAnotherOpenEndedManagerExists()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (selfId, selfEmail) = await CreateManagerAsync(community);
        await CreateManagerAsync(community);
        var selfRow = await RowIdAsync(community, selfId, CommunityRole.CommunityManager);
        using var self = await ClientForAsync(selfEmail);

        var res = await self.PatchAsJsonAsync(MembershipUrl(community, selfRow), new { end = true });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var row = await RowAsync(selfRow);
        Assert.Equal((MembershipStatus.Inactive, (DateOnly?)ClockToday()), (row.Status, row.EndDate));
        // Their very next manager request, on the same session.
        var next = await self.GetAsync($"/api/v1/communities/{community}/memberships");
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(next)).GetProperty("code").GetString());
    }

    // US2-S13
    [Fact]
    public async Task DowngradingACoManagerWhoIsAlsoABoardMember_EndsTheManagerRow_AndKeepsTheBoardRow()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (coId, coEmail) = await CreateManagerAsync(community);
        var boardRow = await WithDbAsync(db => AddMembershipAsync(db, coId, community, CommunityRole.BoardMember));
        var coRow = await RowIdAsync(community, coId, CommunityRole.CommunityManager);
        using var manager = await ClientForAsync(managerEmail);
        // The co-manager signs in before the downgrade and holds manager powers on that session.
        using var co = await ClientForAsync(coEmail);
        Assert.Equal(HttpStatusCode.OK, (await co.GetAsync($"/api/v1/communities/{community}/memberships")).StatusCode);

        var res = await manager.PatchAsJsonAsync(MembershipUrl(community, coRow), new { downgradeTo = (int)CommunityRole.BoardMember });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal((boardRow, "BoardMember", "Active"),
            (body.GetProperty("id").GetGuid(), body.GetProperty("role").GetString(), body.GetProperty("status").GetString()));
        var ended = await RowAsync(coRow);
        Assert.Equal((MembershipStatus.Inactive, (DateOnly?)ClockToday()), (ended.Status, ended.EndDate));
        Assert.Equal(MembershipStatus.Active, (await RowAsync(boardRow)).Status);

        // Their very next manager request, on the same session, is refused; board access stays.
        var next = await co.GetAsync($"/api/v1/communities/{community}/memberships");
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(next)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK,
            (await co.GetAsync($"/api/v1/board/metrics?communityId={community}&surface=CommunityMetrics")).StatusCode);
    }

    // contracts/community-settings.md: the settings page sends the downgrade role by name.
    [Fact]
    public async Task ADowngradeSentByName_IsAccepted()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (coId, _) = await CreateManagerAsync(community);
        var coRow = await RowIdAsync(community, coId, CommunityRole.CommunityManager);
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PatchAsJsonAsync(MembershipUrl(community, coRow), new { downgradeTo = "BoardMember" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("BoardMember", (await JsonAsync(res)).GetProperty("role").GetString());
        Assert.Equal(MembershipStatus.Inactive, (await RowAsync(coRow)).Status);
    }

    // US2-S13, FR-019: a downgrade creates the Board Member row when there is none.
    [Fact]
    public async Task DowngradingACoManagerWithNoBoardRow_CreatesOne()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (coId, _) = await CreateManagerAsync(community);
        var coRow = await RowIdAsync(community, coId, CommunityRole.CommunityManager);
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PatchAsJsonAsync(MembershipUrl(community, coRow), new { downgradeTo = (int)CommunityRole.BoardMember });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var board = await WithDbAsync(db => db.CommunityMemberships.AsNoTracking()
            .SingleAsync(m => m.CommunityId == community && m.UserId == coId && m.Role == CommunityRole.BoardMember));
        Assert.Equal((MembershipStatus.Active, ClockToday(), (DateOnly?)null), (board.Status, board.StartDate, board.EndDate));
    }

    // FR-022 / US2-S15 set-up: an ended or downgraded manager's open invitations are revoked.
    [Theory]
    [InlineData("end")]
    [InlineData("downgrade")]
    public async Task EndingOrDowngradingAManager_RevokesTheInvitationsTheyCanNoLongerSend(string change)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (xId, _) = await CreateManagerAsync(community);
        var xRow = await RowIdAsync(community, xId, CommunityRole.CommunityManager);
        using (var scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<InvitationService>().CreateAsync(new NewInvitation(
                InvitationKind.Membership, companyId, community, null, CommunityRole.BoardMember, null,
                $"lee-{Guid.NewGuid():N}@example.com"), xId, default);
        var invitationId = await WithDbAsync(db => db.Invitations.Where(i => i.SenderUserId == xId).Select(i => i.Id).SingleAsync());
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PatchAsJsonAsync(MembershipUrl(community, xRow),
            change == "end" ? new { end = (bool?)true, downgradeTo = (int?)null } : new { end = (bool?)null, downgradeTo = (int?)(int)CommunityRole.BoardMember });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitationId));
        Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.SenderLostAuthority),
            (invitation.State, invitation.RevokeReason));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("InvitationChange")
            && e.Properties["InvitationId"].ToString() == invitationId.ToString()
            && e.Properties["Reason"].ToString() == "\"SenderLostAuthority\"");
    }

    // The list tells the UI what it may do, and why not.
    [Fact]
    public async Task TheList_SaysWhatCanBeChanged_WithLastManagerAsTheReason()
    {
        var (community, managerId, managerRow, adminEmail) = await OneManagerAsync();
        var (boardId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        using var admin = await ClientForAsync(adminEmail);

        var list = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/memberships"));

        var items = list.GetProperty("items").EnumerateArray().ToList();
        var manager = items.Single(i => i.GetProperty("id").GetGuid() == managerRow);
        Assert.Equal((true, true, false, false, false, "LAST_MANAGER"),
            (manager.GetProperty("openEnded").GetBoolean(), manager.GetProperty("effective").GetBoolean(),
             manager.GetProperty("canEnd").GetBoolean(), manager.GetProperty("canDowngrade").GetBoolean(),
             manager.GetProperty("canEditEndDate").GetBoolean(), manager.GetProperty("reasonCode").GetString()));
        var board = items.Single(i => i.GetProperty("userId").GetString() == boardId);
        Assert.Equal((true, false, true), (board.GetProperty("canEnd").GetBoolean(), board.GetProperty("canDowngrade").GetBoolean(),
            board.GetProperty("canEditEndDate").GetBoolean()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, board.GetProperty("reasonCode").ValueKind);

        // With a second open-ended manager, the first can be ended, downgraded and end-dated.
        await CreateManagerAsync(community);
        var after = (await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/memberships")))
            .GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == managerRow);
        Assert.Equal((true, true, true), (after.GetProperty("canEnd").GetBoolean(), after.GetProperty("canDowngrade").GetBoolean(),
            after.GetProperty("canEditEndDate").GetBoolean()));
    }

    private async Task<(Guid Community, string ManagerId, Guid ManagerRow, string AdminEmail)> OneManagerAsync()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, _) = await CreateManagerAsync(community);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        return (community, managerId, await RowIdAsync(community, managerId, CommunityRole.CommunityManager), adminEmail);
    }

    private Task<Guid> RowIdAsync(Guid community, string userId, CommunityRole role) => WithDbAsync(db =>
        db.CommunityMemberships.Where(m => m.CommunityId == community && m.UserId == userId && m.Role == role)
            .Select(m => m.Id).SingleAsync());

    private Task<Domain.Entities.CommunityMembership> RowAsync(Guid id) =>
        WithDbAsync(db => db.CommunityMemberships.AsNoTracking().SingleAsync(m => m.Id == id));

    private static string MembershipUrl(Guid community, Guid membershipId) =>
        $"/api/v1/communities/{community}/memberships/{membershipId}";

    // The date the app stamps: the test clock's, not the wall clock's.
    private DateOnly ClockToday() => DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime);
}

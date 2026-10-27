using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T093 — US3-S2: an Onboarding community's members work in it with their normal
/// capabilities (FR-011): the manager staffs it and sets its ARC rules, the board reads it, and a
/// Board Member still can't edit the ARC settings.
/// </summary>
public class OnboardingCommunityWorkTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US3-S2
    [Fact]
    public async Task MembersOfAnOnboardingCommunity_WorkWithTheirNormalCapabilities()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (personId, _) = await CreateStaffAsync(community, CommunityRole.Accountant);
        using var manager = await ClientForAsync(managerEmail);
        var settings = new
        {
            reviewPeriodDays = 45, lapseRule = "FlagOverdueOnly", decisionRule = "MajorityOfMembers", reminderDays = 7,
            timeZoneId = "America/New_York", formalDisapprovalStatement = "The board does not approve this application.",
        };

        var grant = await manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.BoardMember, userId = personId });
        var save = await manager.PutAsJsonAsync($"/api/v1/communities/{community}/architectural-settings", settings);

        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.True(await WithDbAsync(db => db.CommunityMemberships.AnyAsync(m => m.CommunityId == community && m.UserId == personId
            && m.Role == CommunityRole.BoardMember && m.Status == MembershipStatus.Active)));

        // A Board Member with no other role here, so the reads below rest on the Board Member role alone.
        var (_, boardOnlyEmail) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        using var board = await ClientForAsync(boardOnlyEmail);
        var metrics = await board.GetAsync($"/api/v1/board/metrics?communityId={community}&surface=CommunityMetrics");
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);

        var boardSave = await board.PutAsJsonAsync($"/api/v1/communities/{community}/architectural-settings", settings);
        Assert.Equal(HttpStatusCode.Forbidden, boardSave.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(boardSave)).GetProperty("code").GetString());

        // The same refusal as in an Active community.
        var active = await CreateCommunityInAsync(companyId);
        var (_, activeBoardEmail) = await CreateStaffAsync(active, CommunityRole.BoardMember);
        using var activeBoard = await ClientForAsync(activeBoardEmail);
        var activeSave = await activeBoard.PutAsJsonAsync($"/api/v1/communities/{active}/architectural-settings", settings);
        Assert.Equal(boardSave.StatusCode, activeSave.StatusCode);
        Assert.Equal(await boardSave.Content.ReadAsStringAsync(), await activeSave.Content.ReadAsStringAsync());
    }
}

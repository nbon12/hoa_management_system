using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T136 — US7-S9 (FR-003): the Company Administrator role administers communities but gives no
/// board powers. Without a Board Member membership in A, every architectural review and board
/// data route refuses them, as it does anyone else outside the board.
/// </summary>
public class CompanyAdminNoBoardPowersTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public static TheoryData<string, string> BoardRoutes => new()
    {
        { "POST", "/architectural-applications/{open}/votes" },
        { "POST", "/architectural-applications/{reached}/outcome" },
        { "POST", "/architectural-applications/{closed}/outcome/resend-email" },
        { "GET", "/architectural-applications" },
        { "GET", "/architectural-applications/{open}" },
        { "GET", "metrics" },
    };

    // US7-S9
    [Theory]
    [MemberData(nameof(BoardRoutes))]
    public async Task ACompanyAdministratorWithNoBoardSeat_GetsTheSame403AsAnOutsider(string method, string route)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var open = await CreateArcApplicationAsync(community, home, 1001);
        var reached = await CreateArcApplicationAsync(community, home, 1002, ArcApplicationStatus.DecisionReached);
        var closed = await CreateArcApplicationAsync(community, home, 1003, ArcApplicationStatus.Closed);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var path = route == "metrics"
            ? $"/api/v1/board/metrics?communityId={community}&surface=CommunityMetrics"
            : $"/api/v1/communities/{community}" + route.Replace("{open}", open.ToString())
                .Replace("{reached}", reached.ToString()).Replace("{closed}", closed.ToString());

        var res = method == "GET"
            ? await admin.GetAsync(path)
            : await admin.PostAsJsonAsync(path, route.EndsWith("votes") ? new { choice = "Approve" } : new object());

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(0, await WithDbAsync(db => db.ArchitecturalVotes.CountAsync(v => v.ApplicationId == open)));
        Assert.Equal(ArcApplicationStatus.DecisionReached,
            await WithDbAsync(db => db.ArchitecturalApplications.Where(a => a.Id == reached).Select(a => a.Status).SingleAsync()));
    }
}

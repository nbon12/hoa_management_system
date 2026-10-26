using System.Diagnostics;
using System.Net;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Xunit;

namespace HOAManagementCompany.Tests.Performance;

/// <summary>
/// 030 T142 — SC-008: the portfolio's first page answers in under 2 seconds for a company with 200
/// communities, each with managers, a pending manager invitation and its ARC settings. Bulk-inserted;
/// the first page is timed (median of 5).
/// </summary>
public class PortfolioPerformanceTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task FirstPage_For200Communities_IsUnder2Seconds()
    {
        var companyId = await CreateCompanyAsync();
        var managerId = await WithDbAsync(CreateUserAsync);
        var otherManagerId = await WithDbAsync(CreateUserAsync);
        await WithDbAsync(async db =>
        {
            var start = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddYears(-1);
            for (var i = 0; i < 200; i++)
            {
                var id = Guid.NewGuid();
                db.Communities.Add(new Community
                {
                    Id = id, ManagementCompanyId = companyId, CommunityName = $"Community {i}", LegalName = $"Community {i}, Inc.",
                    Status = (CommunityStatus)(i % 3), ActivatedAt = i % 3 == 0 ? null : Clock.UtcNow.AddMonths(-6),
                });
                var settings = CommunityArcSettings.Defaults(id);
                if (i % 4 == 0) settings.ReviewPeriodDays = 45;
                db.CommunityArcSettings.Add(settings);
                db.CommunityMemberships.Add(new CommunityMembership
                {
                    Id = Guid.NewGuid(), CommunityId = id, UserId = managerId, Role = CommunityRole.CommunityManager,
                    Status = MembershipStatus.Active, StartDate = start, EndDate = i % 5 == 0 ? start.AddYears(2) : null,
                });
                db.CommunityMemberships.Add(new CommunityMembership
                {
                    Id = Guid.NewGuid(), CommunityId = id, UserId = otherManagerId, Role = CommunityRole.BoardMember,
                    Status = MembershipStatus.Active, StartDate = start,
                });
                db.Invitations.Add(new Invitation
                {
                    Id = Guid.NewGuid(), Kind = InvitationKind.Membership, CompanyId = companyId, CommunityId = id,
                    Role = CommunityRole.CommunityManager, Email = $"pat{i}@example.com", NormalizedEmail = $"pat{i}@example.com",
                    State = InvitationState.Pending, SenderUserId = managerId, CreatedAt = Clock.UtcNow, SentAt = Clock.UtcNow,
                    ExpiresAt = Clock.UtcNow.AddHours(72), TokenHash = Guid.NewGuid().ToString("N"),
                });
            }
            return await db.SaveChangesAsync();
        });
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        (await admin.GetAsync("/api/v1/company/communities")).EnsureSuccessStatusCode(); // warm-up

        var times = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await admin.GetAsync("/api/v1/company/communities");
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            times.Add(sw.ElapsedMilliseconds);
        }
        times.Sort();
        Assert.True(times[2] < 2000, $"median first-page time {times[2]}ms (threshold 2000ms)");
    }
}

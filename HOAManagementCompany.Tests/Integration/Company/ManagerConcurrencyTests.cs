using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T079 — US2-S14: two open-ended managers end each other at the same moment. The rule runs
/// under the community lock, so exactly one wins and the community keeps an open-ended manager.
/// </summary>
public class ManagerConcurrencyTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US2-S14
    [Fact]
    public async Task TwoManagersEndingEachOther_ExactlyOneWins_EveryTime()
    {
        for (var round = 0; round < 20; round++)
        {
            var companyId = await CreateCompanyAsync();
            var community = await CreateCommunityInAsync(companyId);
            var (aId, _) = await CreateManagerAsync(community);
            var (bId, _) = await CreateManagerAsync(community);
            var rows = await WithDbAsync(db => db.CommunityMemberships.Where(m => m.CommunityId == community)
                .ToDictionaryAsync(m => m.UserId, m => m.Id));
            // Minted tokens, so 20 rounds don't trip the sign-in rate limit.
            using var a = ClientWithToken(MintToken(aId));
            using var b = ClientWithToken(MintToken(bId));

            var start = new TaskCompletionSource();
            var aEndsB = Task.Run(async () => { await start.Task; return await a.PatchAsJsonAsync(Url(community, rows[bId]), new { end = true }); });
            var bEndsA = Task.Run(async () => { await start.Task; return await b.PatchAsJsonAsync(Url(community, rows[aId]), new { end = true }); });
            start.SetResult();
            var results = await Task.WhenAll(aEndsB, bEndsA);

            var statuses = results.Select(r => r.StatusCode).OrderBy(s => (int)s).ToList();
            Assert.True(statuses.SequenceEqual([HttpStatusCode.OK, (HttpStatusCode)422]), $"round {round}: {string.Join(", ", statuses)}");
            var refused = results.Single(r => r.StatusCode == (HttpStatusCode)422);
            Assert.Equal("LAST_MANAGER", (await JsonAsync(refused)).GetProperty("code").GetString());
            Assert.Equal(1, await WithDbAsync(db => db.CommunityMemberships.CountAsync(m => m.CommunityId == community
                && m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active && m.EndDate == null)));
        }
    }

    private static string Url(Guid community, Guid membershipId) =>
        $"/api/v1/communities/{community}/memberships/{membershipId}";
}

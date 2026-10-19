using System.Net;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T134 — US7-S4: two Company Administrators revoke each other at the same moment. The rule
/// runs under the company lock, so exactly one wins and one live grant is left.
/// </summary>
public class CompanyAdminConcurrencyTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    // US7-S4
    [Fact]
    public async Task TwoAdministratorsRevokingEachOther_ExactlyOneWins_EveryTime()
    {
        for (var round = 0; round < 20; round++)
        {
            var companyId = await CreateCompanyAsync();
            var (aId, _) = await CreateCompanyAdminAsync(companyId);
            var (bId, _) = await CreateCompanyAdminAsync(companyId);
            // Minted tokens, so 20 rounds don't trip the sign-in rate limit.
            using var a = ClientWithToken(MintToken(aId));
            using var b = ClientWithToken(MintToken(bId));

            using var barrier = new Barrier(2);
            var aRevokesB = Task.Run(async () => { barrier.SignalAndWait(); return await a.PostAsync($"/api/v1/company/administrators/{bId}/revoke", null); });
            var bRevokesA = Task.Run(async () => { barrier.SignalAndWait(); return await b.PostAsync($"/api/v1/company/administrators/{aId}/revoke", null); });
            var results = await Task.WhenAll(aRevokesB, bRevokesA);

            var statuses = results.Select(r => r.StatusCode).OrderBy(s => (int)s).ToList();
            Assert.True(statuses.SequenceEqual([HttpStatusCode.OK, (HttpStatusCode)422]), $"round {round}: {string.Join(", ", statuses)}");
            var refused = results.Single(r => r.StatusCode == (HttpStatusCode)422);
            Assert.Equal("LAST_COMPANY_ADMIN", (await JsonAsync(refused)).GetProperty("code").GetString());
            Assert.Equal(1, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync(g => g.CompanyId == companyId && g.RevokedAt == null)));
        }
    }
}

using System.Security.Claims;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 R1: <see cref="CompanyContext"/> resolves the company for one request and caches each answer
/// for the rest of it.
/// </summary>
public class CompanyContextTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task APrincipalWithNoUserId_BelongsToNoCompany()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Null(await WithDbAsync(db => new CompanyContext(db).ForUserAsync(anonymous)));
    }

    [Fact]
    public async Task TheCallersCompany_IsAnsweredOncePerRequest()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var caller = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", adminId)], "test"));

        await WithDbAsync(async db =>
        {
            var request = new CompanyContext(db);
            Assert.Equal(companyId, await request.ForUserAsync(caller));

            await db.CompanyAdministratorGrants.Where(g => g.UserId == adminId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, DateTimeOffset.UtcNow));

            Assert.Equal(companyId, await request.ForUserAsync(caller));
            Assert.Null(await new CompanyContext(db).ForUserAsync(caller));
        });
    }

    [Fact]
    public async Task ACommunitysCompany_IsAnsweredOncePerRequest()
    {
        var companyId = await CreateCompanyAsync();
        var otherCompanyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);

        await WithDbAsync(async db =>
        {
            var request = new CompanyContext(db);
            Assert.Equal(companyId, await request.ForCommunityAsync(community));
            Assert.Null(await request.ForCommunityAsync(Guid.NewGuid()));

            await db.Communities.Where(c => c.Id == community)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ManagementCompanyId, otherCompanyId));

            Assert.Equal(companyId, await request.ForCommunityAsync(community));
            Assert.Equal(otherCompanyId, await new CompanyContext(db).ForCommunityAsync(community));
        });
    }
}

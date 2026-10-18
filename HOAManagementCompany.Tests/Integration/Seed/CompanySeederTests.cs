using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Seed;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Seed;

/// <summary>
/// 030 T060 — the dev company seed (quickstart.md): <c>company@nekohoa.dev</c> made a Company
/// Administrator through the setup step, and the Onboarding community Keystone Ridge with
/// <c>manager@nekohoa.dev</c> as its open-ended Community Manager. Runs on its own fresh database,
/// because it writes to the deployment's single company.
/// </summary>
public class CompanySeederTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private string? _database;

    protected override string DatabaseConnectionString() =>
        _database ??= Task.Run(() => Fixture.CreateFreshDatabaseAsync()).GetAwaiter().GetResult();

    [Fact]
    public async Task InADevLikeEnvironment_SeedsTheCompanyAdministratorAndKeystoneRidge_Once()
    {
        await SeedAsync(Environments.Development);
        var first = await SnapshotAsync();
        await SeedAsync(Environments.Development);

        Assert.Equal(first, await SnapshotAsync());
        await WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Email == "company@nekohoa.dev");
            var grant = await db.CompanyAdministratorGrants.SingleAsync(g => g.UserId == admin.Id && g.RevokedAt == null);
            Assert.Equal(CompanyGrantSource.Setup, grant.Source);
            Assert.False(await db.UserProperties.AnyAsync(up => up.UserId == admin.Id));
            Assert.False(await db.CommunityMemberships.AnyAsync(m => m.UserId == admin.Id));

            var ridge = await db.Communities.SingleAsync(c => c.CommunityName == "Keystone Ridge");
            Assert.Equal((CommunityStatus.Onboarding, "Keystone Ridge HOA, Inc.", grant.CompanyId),
                (ridge.Status, ridge.LegalName, ridge.ManagementCompanyId));
            var manager = await db.CommunityMemberships.Include(m => m.User).SingleAsync(m => m.CommunityId == ridge.Id);
            Assert.Equal(("manager@nekohoa.dev", CommunityRole.CommunityManager, MembershipStatus.Active, (DateOnly?)null),
                (manager.User.Email, manager.Role, manager.Status, manager.EndDate));
            Assert.True(await db.Properties.CountAsync(p => p.CommunityId == ridge.Id) >= 2);
            Assert.True(await db.CommunityArcSettings.AnyAsync(s => s.CommunityId == ridge.Id));
        });

        // The seeded administrator signs in with no home, to the company area.
        var login = await CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "company@nekohoa.dev", password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Company", (await JsonAsync(login)).GetProperty("user").GetProperty("landing").GetString());
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task OutsideDevLikeEnvironments_SeedsNothing(string environment)
    {
        var before = await SnapshotAsync();

        await SeedAsync(environment);

        Assert.Equal(before, await SnapshotAsync());
    }

    private async Task SeedAsync(string environment)
    {
        using var scope = NewScope();
        var sp = scope.ServiceProvider;
        await new CompanySeeder(sp.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>(), sp,
                new FakeEnvironment(environment), sp.GetRequiredService<ILogger<DatabaseSeeder>>())
            .SeedAsync();
    }

    private Task<string> SnapshotAsync() => WithDbAsync(async db => string.Join("|",
        await db.Users.CountAsync(),
        await db.CompanyAdministratorGrants.CountAsync(),
        await db.Communities.CountAsync(),
        await db.CommunityMemberships.CountAsync(),
        await db.Properties.CountAsync(),
        await db.Invitations.CountAsync(),
        await db.CommunityArcSettings.CountAsync()));

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "HOAManagementCompany";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

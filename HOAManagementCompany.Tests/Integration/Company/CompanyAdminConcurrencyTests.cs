using System.Data.Common;
using System.Net;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T134 — US7-S4: two Company Administrators revoke each other at the same moment. The rule
/// runs under the company lock, so exactly one wins and one live grant is left.
/// </summary>
public class CompanyAdminConcurrencyTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private readonly BeforeTakingTheLock _beforeLock = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.ConfigureDbContext<ApplicationDbContext>(o => o.AddInterceptors(_beforeLock));
    }

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

    // US7-S4 with a third administrator: B revokes A while A's revoke of B waits for the company
    // lock. A's grant still counts toward the rule, so B is refused for having lost their own.
    [Fact]
    public async Task ARevokerWhoseOwnGrantIsRevokedWhileWaitingForTheLock_IsForbidden_AndTheTargetKeepsTheirs()
    {
        var companyId = await CreateCompanyAsync();
        var (aId, _) = await CreateCompanyAdminAsync(companyId);
        var (bId, _) = await CreateCompanyAdminAsync(companyId);
        await CreateCompanyAdminAsync(companyId);
        using var b = ClientWithToken(MintToken(bId));
        _beforeLock.Once = () => WithDbAsync(db => db.CompanyAdministratorGrants.Where(g => g.UserId == bId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, Clock.UtcNow).SetProperty(g => g.RevokedByUserId, aId)));

        var res = await b.PostAsync($"/api/v1/company/administrators/{aId}/revoke", null);

        Assert.Null(_beforeLock.Once);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await WithDbAsync(db => db.CompanyAdministratorGrants.Where(g => g.UserId == aId).Select(g => g.RevokedAt).SingleAsync()));
        Assert.Equal(2, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync(g => g.CompanyId == companyId && g.RevokedAt == null)));
        Assert.DoesNotContain(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange"));
    }

    /// <summary>Runs <see cref="Once"/> just before the next advisory lock is taken, once armed.</summary>
    private sealed class BeforeTakingTheLock : DbCommandInterceptor
    {
        private Func<Task>? _once;

        public Func<Task>? Once { get => Volatile.Read(ref _once); set => Volatile.Write(ref _once, value); }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock") && Interlocked.Exchange(ref _once, null) is { } run)
                await run();
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

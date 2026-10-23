using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T027 — the per-community and per-company advisory locks (R5) serialize bodies that share an
/// id and leave different ids free to run together.
/// </summary>
public class AdvisoryLockTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(600);

    public delegate Task<int> Locker(ApplicationDbContext db, Guid id, Func<Task<int>> body, CancellationToken ct);

    public static IEnumerable<object[]> Lockers() =>
    [
        [new Locker(CommunityLocks.RunAsync)],
        [new Locker(CompanyLocks.RunAsync)],
    ];

    [Theory]
    [MemberData(nameof(Lockers))]
    public async Task SameId_Serializes(Locker locker)
    {
        var id = Guid.NewGuid();
        var (first, second) = await RunPairAsync(locker, id, id);

        // The second body starts only after the first committed.
        Assert.True(second.Start >= first.End,
            $"expected serialization: first ended {first.End:O}, second started {second.Start:O}");
    }

    [Theory]
    [MemberData(nameof(Lockers))]
    public async Task DifferentIds_RunConcurrently(Locker locker)
    {
        var (first, second) = await RunPairAsync(locker, Guid.NewGuid(), Guid.NewGuid());

        Assert.True(second.Start < first.End,
            $"expected overlap: first ended {first.End:O}, second started {second.Start:O}");
    }

    [Fact]
    public async Task ThrowingBody_RollsBack_AndReleasesTheLock()
    {
        var id = Guid.NewGuid();
        var companyId = await CreateCompanyAsync();
        using (var scope = NewScope())
        {
            var db = Db(scope);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CommunityLocks.RunAsync<int>(db, id, async () =>
            {
                db.ManagementCompanies.Single(c => c.Id == companyId).Name = "renamed inside a failed lock";
                await db.SaveChangesAsync();
                throw new InvalidOperationException("boom");
            }, CancellationToken.None));
        }

        Assert.NotEqual("renamed inside a failed lock",
            await WithDbAsync(db => Task.FromResult(db.ManagementCompanies.Single(c => c.Id == companyId).Name)));

        // The lock was released with the rolled-back transaction.
        using var again = NewScope();
        Assert.Equal(1, await CommunityLocks.RunAsync(Db(again), id, () => Task.FromResult(1), CancellationToken.None));
    }

    private async Task<((DateTimeOffset Start, DateTimeOffset End) First, (DateTimeOffset Start, DateTimeOffset End) Second)>
        RunPairAsync(Locker locker, Guid firstId, Guid secondId)
    {
        (DateTimeOffset Start, DateTimeOffset End) a = default, b = default;
        using var scopeA = NewScope();
        using var scopeB = NewScope();
        var firstEntered = new TaskCompletionSource();

        var first = locker(Db(scopeA), firstId, async () =>
        {
            a.Start = DateTimeOffset.UtcNow;
            firstEntered.SetResult();
            await Task.Delay(Hold);
            a.End = DateTimeOffset.UtcNow;
            return 1;
        }, CancellationToken.None);

        await firstEntered.Task;
        var second = locker(Db(scopeB), secondId, () =>
        {
            b.Start = DateTimeOffset.UtcNow;
            b.End = b.Start;
            return Task.FromResult(2);
        }, CancellationToken.None);

        await Task.WhenAll(first, second);
        return (a, b);
    }
}

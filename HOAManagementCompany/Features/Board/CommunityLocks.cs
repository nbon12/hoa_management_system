using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// 030 R5: serializes changes that must see each other's committed state — manager memberships,
// invitation accept/revoke/resend, and activate/archive/restore — per community. A Postgres
// transaction-scoped advisory lock (two-int key space, so it never meets LedgerService's bigint
// keys) inside the retrying execution strategy, as ArcLocks and the ledger do. The body runs in
// one transaction: it commits when the body returns and rolls back when it throws.
public static class CommunityLocks
{
    internal const int KeySpace = 30_001;

    public static Task<T> RunAsync<T>(ApplicationDbContext db, Guid communityId, Func<Task<T>> body, CancellationToken ct) =>
        AdvisoryLock.RunAsync(db, KeySpace, communityId, body, ct);
}

internal static class AdvisoryLock
{
    public static Task<T> RunAsync<T>(ApplicationDbContext db, int keySpace, Guid id, Func<Task<T>> body, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}, {1})", [keySpace, KeyOf(id)], ct);
            var result = await body();
            await tx.CommitAsync(ct);
            return result;
        });
    }

    // A stable 32-bit key for a GUID. Two ids sharing a key only serialize, never interfere.
    internal static int KeyOf(Guid id)
    {
        var bytes = id.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4)
             ^ BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);
    }
}

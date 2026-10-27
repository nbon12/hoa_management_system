using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// 030 R5: serializes changes that must see each other's committed state — manager memberships,
// invitation accept/revoke/resend, and activate/archive/restore — per community. A Postgres
// transaction-scoped advisory lock (two-int key space, so it never meets LedgerService's bigint
// keys) inside the retrying execution strategy, as ArcLocks and the ledger do. The body runs in
// one transaction: it commits when the body returns and rolls back when it throws. Change events
// the body writes are written after the commit (PendingEvents).
public static class CommunityLocks
{
    internal const int KeySpace = 30_001;

    public static Task<T> RunAsync<T>(ApplicationDbContext db, Guid communityId, Func<Task<T>> body, CancellationToken ct) =>
        AdvisoryLock.RunAsync(db, KeySpace, communityId, body, ct);
}

internal static class AdvisoryLock
{
    public static async Task<T> RunAsync<T>(ApplicationDbContext db, int keySpace, Guid id, Func<Task<T>> body, CancellationToken ct)
    {
        List<Action> events = [];
        var attempts = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            // A retry starts from the database: the failed attempt's saves were accepted into the
            // tracker but rolled back, so tracked entities would hide its changes from this attempt.
            if (attempts++ > 0)
                db.ChangeTracker.Clear();
            // Each attempt starts its own batch, so a retried attempt drops the failed one's events.
            events = PendingEvents.Begin();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}, {1})", [keySpace, KeyOf(id)], ct);
            var value = await body();
            try
            {
                await tx.CommitAsync(ct);
            }
            catch
            {
                // The commit's outcome is unknown, so its changes may have landed: write their events
                // rather than lose them. A retry that applies them again writes its own as well.
                foreach (var write in events)
                    write();
                events = [];
                throw;
            }
            return value;
        });
        foreach (var write in events)
            write();
        return result;
    }

    // A stable 32-bit key for a GUID. Two ids sharing a key only serialize, never interfere.
    internal static int KeyOf(Guid id)
    {
        var bytes = id.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4)
             ^ BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);
    }
}

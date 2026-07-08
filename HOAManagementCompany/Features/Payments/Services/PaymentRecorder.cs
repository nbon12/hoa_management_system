using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Services;

// <!-- REPOWISE:START domain=payments -->
// Shared atomic settle path (015 FR-001..FR-003): every flow that records payment effects —
// one-time confirm, recurring draft, webhook/reconcile settlement — runs its writes through one
// of these two units so transaction row, ledger entries, receipt, alerts, and status commit
// all-or-nothing. ApplyAsync re-reads the row FOR UPDATE and re-evaluates the caller's guard
// inside the transaction, making retries and concurrent duplicate deliveries exactly-once.
// <!-- REPOWISE:END -->

/// <summary>
/// The single sanctioned way to persist payment effects. Wraps the retrying execution strategy +
/// an explicit transaction (Npgsql forbids a bare user transaction under EnableRetryOnFailure),
/// keeps transactions short (no external/provider calls may happen inside a unit — resolve them
/// first, pass the results in), and guarantees:
/// <list type="bullet">
///   <item>all-or-nothing visibility of a business event's effects (FR-001);</item>
///   <item>idempotency guards that commit with the work they guard, on a row-locked read, so a
///   retry or a concurrent duplicate delivery cannot re-apply effects (FR-002);</item>
///   <item>clean re-runs under the execution strategy (a half-populated change tracker from a
///   failed attempt is reset before the next attempt).</item>
/// </list>
/// </summary>
public sealed class PaymentRecorder(ApplicationDbContext db)
{
    /// <summary>
    /// Records a NEW transaction plus whatever the flow adds around it (ledger entry, receipt,
    /// draft row) as one atomic unit. <paramref name="completeUnit"/> runs after the transaction
    /// row is flushed (so ledger appends can reference it) and inside the same DB transaction;
    /// it is re-invoked on execution-strategy retries and must reset any closure state it builds.
    /// </summary>
    public Task RecordNewAsync(
        PaymentTransaction txn,
        Func<CancellationToken, Task>? completeUnit = null,
        CancellationToken ct = default) =>
        ExecuteUnitAsync(async innerCt =>
        {
            db.PaymentTransactions.Add(txn);
            await db.SaveChangesAsync(innerCt);
            if (completeUnit is not null) await completeUnit(innerCt);
        }, ct);

    /// <summary>
    /// Applies an event to an EXISTING transaction as one atomic unit. The row is re-read under
    /// <c>FOR UPDATE</c> and reloaded once the lock is held, so <paramref name="apply"/> always
    /// sees committed-current state: terminal-status/cumulative-amount guards evaluated inside
    /// <paramref name="apply"/> are race-free — a concurrent duplicate delivery blocks on the row
    /// lock and then sees the first delivery's outcome. No-ops when the id is unknown.
    /// </summary>
    public Task ApplyAsync(
        Guid transactionId,
        Func<PaymentTransaction, CancellationToken, Task> apply,
        CancellationToken ct = default) =>
        ExecuteUnitAsync(async innerCt =>
        {
            var txn = await db.PaymentTransactions
                .FromSql($"""SELECT * FROM "PaymentTransactions" WHERE "Id" = {transactionId} FOR UPDATE""")
                .FirstOrDefaultAsync(innerCt);
            if (txn is null) return;
            // The FOR UPDATE query returns the already-tracked instance when one exists; reload
            // so the guard sees the values committed by whoever held the lock before us.
            await db.Entry(txn).ReloadAsync(innerCt);

            await apply(txn, innerCt);
            txn.UpdatedAt = DateTimeOffset.UtcNow;
        }, ct);

    private async Task ExecuteUnitAsync(Func<CancellationToken, Task> unit, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A previous attempt may have left Added entities (ledger entries, receipts, outbox
            // rows) in the tracker; detach them so the re-run starts from a clean slate instead
            // of double-inserting. Callers must not hold unflushed Added entities of their own
            // when invoking a unit — the unit owns its inserts.
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await unit(ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}

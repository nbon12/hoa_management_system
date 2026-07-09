using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Ledger;

/// <summary>
/// Append-only accounting ledger (FR-007d/e, SC-009). Entries are never mutated or deleted —
/// corrections are compensating entries. Each property has a monotonic <c>Sequence</c> assigned
/// under a per-property advisory lock so concurrent/out-of-order webhook-driven inserts cannot
/// race, and <c>RunningBalance</c> is always recomputed deterministically by that sequence.
/// </summary>
public sealed class LedgerService(ApplicationDbContext db)
{
    /// <summary>
    /// Appends one entry with the next per-property <c>Sequence</c> and a deterministic
    /// <c>RunningBalance = previous + Charge − Payment</c>. Runs inside a transaction holding a
    /// PostgreSQL advisory lock keyed by the property so the sequence stays gap-safe under
    /// concurrency. Joins the caller's ambient transaction when one already exists.
    /// </summary>
    public async Task<LedgerEntry> AppendAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        // Already inside the caller's transaction (e.g. ConfirmPaymentEndpoint): join it — the caller
        // owns commit/rollback and the surrounding execution strategy. The advisory lock + insert just
        // participate in that ambient transaction.
        if (db.Database.CurrentTransaction is not null)
        {
            await AppendCoreAsync(entry, ct);
            return entry;
        }

        // Standalone (e.g. webhook/reconciliation sweep): own a transaction, run it through the retrying
        // execution strategy so a transient Neon disconnect retries the whole advisory-locked unit
        // (EnableRetryOnFailure forbids a bare user-initiated transaction otherwise).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await AppendCoreAsync(entry, ct);
            await tx.CommitAsync(ct);
        });
        return entry;
    }

    /// <summary>
    /// The advisory-locked append itself, assuming a transaction is already in effect. Acquires the
    /// per-property lock, computes the next <c>Sequence</c> and deterministic <c>RunningBalance</c>,
    /// and persists the row.
    /// </summary>
    private async Task AppendCoreAsync(LedgerEntry entry, CancellationToken ct)
    {
        await AcquirePropertyLockAsync(entry.PropertyId, ct);

        var last = await db.LedgerEntries
            .Where(e => e.PropertyId == entry.PropertyId)
            .OrderByDescending(e => e.Sequence)
            .Select(e => new { e.Sequence, e.RunningBalance })
            .FirstOrDefaultAsync(ct);

        entry.Sequence = (last?.Sequence ?? 0) + 1;
        entry.RunningBalance = (last?.RunningBalance ?? 0m) + entry.ChargeAmount - entry.PaymentAmount;
        if (entry.CreatedAt == default) entry.CreatedAt = DateTimeOffset.UtcNow;

        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Records a payment against the property's balance, linked to its audit transaction.
    /// A payment larger than the amount owed drives <c>RunningBalance</c> negative — that negative
    /// is the resident's credit balance (FR-007c); no separate row is needed for the math.
    /// </summary>
    public Task<LedgerEntry> AddPaymentAsync(
        Guid propertyId, Guid transactionId, decimal amount, string description,
        DateOnly? entryDate = null, string? documentNumber = null, CancellationToken ct = default) =>
        AppendAsync(new LedgerEntry
        {
            PropertyId = propertyId,
            TransactionId = transactionId,
            EntryDate = entryDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DocumentNumber = documentNumber,
            Description = description,
            PaymentAmount = amount,
            EntryType = LedgerEntryType.Payment,
        }, ct);

    /// <summary>
    /// Adds a compensating charge entry (refund, reversal, chargeback, returned-payment/NSF fee,
    /// adjustment) — the append-only correction for a reversal of funds (FR-014a/b/c/d).
    /// </summary>
    public Task<LedgerEntry> AddCompensatingChargeAsync(
        Guid propertyId, Guid? transactionId, decimal amount, LedgerEntryType type, string description,
        string? documentNumber = null, CancellationToken ct = default) =>
        AppendAsync(new LedgerEntry
        {
            PropertyId = propertyId,
            TransactionId = transactionId,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DocumentNumber = documentNumber,
            Description = description,
            ChargeAmount = amount,
            EntryType = type,
        }, ct);

    /// <summary>Current property balance (positive = owed, negative = credit), by latest sequence.</summary>
    public async Task<decimal> GetCurrentBalanceAsync(Guid propertyId, CancellationToken ct = default) =>
        await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId)
            .OrderByDescending(e => e.Sequence)
            .Select(e => e.RunningBalance)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Deterministically recomputes every <c>RunningBalance</c> for a property in <c>Sequence</c>
    /// order — the canonical repair for out-of-order ACH settlements/refunds (SC-009).
    /// Holds the same per-property advisory lock as appends (015 FR-004), so a concurrent
    /// <see cref="AppendAsync"/> cannot base its balance on a value this repair is rewriting.
    /// Returns the final balance.
    /// </summary>
    public async Task<decimal> RecomputeBalancesAsync(Guid propertyId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null)
            return await RecomputeCoreAsync(propertyId, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var result = 0m;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            result = await RecomputeCoreAsync(propertyId, ct);
            await tx.CommitAsync(ct);
        });
        return result;
    }

    private async Task<decimal> RecomputeCoreAsync(Guid propertyId, CancellationToken ct)
    {
        await AcquirePropertyLockAsync(propertyId, ct);

        var entries = await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        decimal running = 0m;
        foreach (var e in entries)
        {
            running += e.ChargeAmount - e.PaymentAmount;
            e.RunningBalance = running;
        }
        await db.SaveChangesAsync(ct);
        return running;
    }

    private Task AcquirePropertyLockAsync(Guid propertyId, CancellationToken ct)
    {
        // Stable 64-bit key from the property id; xact lock auto-releases at commit/rollback.
        var key = unchecked((long)(ulong)propertyId.GetHashCode() ^ ((long)propertyId.ToByteArray()[0] << 32));
        return db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", new object[] { key }, ct);
    }
}

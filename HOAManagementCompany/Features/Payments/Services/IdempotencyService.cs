using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Services;

/// <summary>
/// Durable payment-initiation idempotency (FR-007a, FR-035). A client-supplied key is persisted on
/// <see cref="PaymentTransaction.IdempotencyKey"/> (unique) and forwarded to Stripe via
/// <c>RequestOptions.IdempotencyKey</c>, so a double-submit collapses to a single charge and a
/// replay returns the original transaction. Survives restarts (persisted in PostgreSQL).
/// </summary>
public sealed class IdempotencyService(ApplicationDbContext db)
{
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Returns the original transaction for this key, or null if first-seen.</summary>
    public Task<PaymentTransaction?> FindExistingAsync(Guid propertyId, string? idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return Task.FromResult<PaymentTransaction?>(null);
        return db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PropertyId == propertyId && t.IdempotencyKey == idempotencyKey, ct);
    }
}

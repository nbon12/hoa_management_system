using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Payments.Ledger;

/// <summary>An open charge eligible for payment allocation.</summary>
public sealed record OpenCharge(Guid Id, LedgerEntryType Category, DateOnly Date, decimal Amount);

/// <summary>How much of a payment was applied to one charge.</summary>
public sealed record ChargeAllocation(Guid ChargeId, LedgerEntryType Category, decimal Applied);

/// <summary>Outcome of allocating a payment across open charges.</summary>
public sealed record AllocationResult(IReadOnlyList<ChargeAllocation> Allocations, decimal Surplus);

/// <summary>
/// Applies a payment across open charges by statutory category priority then oldest-first within a
/// category (FR-007b). Surplus beyond all open charges becomes a credit (FR-007c). The priority
/// order is configurable per HOA via <see cref="HoaPaymentConfig.AllocationOrderJson"/>.
/// </summary>
public sealed class AllocationService(LedgerService ledger)
{
    /// <summary>Default statutory order when a config provides none.</summary>
    public static readonly IReadOnlyList<LedgerEntryType> DefaultOrder = new[]
    {
        LedgerEntryType.RegularAssessment,
        LedgerEntryType.LateFee,
        LedgerEntryType.FinanceCharge,
        LedgerEntryType.ReturnedPaymentFee,
        LedgerEntryType.Adjustment,
    };

    /// <summary>Parses the per-HOA allocation order, falling back to <see cref="DefaultOrder"/>.</summary>
    public static IReadOnlyList<LedgerEntryType> ParseOrder(HoaPaymentConfig? config)
    {
        if (config is null || string.IsNullOrWhiteSpace(config.AllocationOrderJson)) return DefaultOrder;
        try
        {
            var names = JsonSerializer.Deserialize<string[]>(config.AllocationOrderJson);
            var parsed = names?
                .Select(n => Enum.TryParse<LedgerEntryType>(n, true, out var t) ? (LedgerEntryType?)t : null)
                .Where(t => t is not null)
                .Select(t => t!.Value)
                .ToList();
            return parsed is { Count: > 0 } ? parsed : DefaultOrder;
        }
        catch (JsonException)
        {
            return DefaultOrder;
        }
    }

    /// <summary>
    /// Pure allocation: distributes <paramref name="payment"/> across <paramref name="charges"/> by
    /// category priority then oldest-first, returning the per-charge split and any surplus.
    /// </summary>
    public static AllocationResult Allocate(
        IReadOnlyList<OpenCharge> charges, decimal payment, IReadOnlyList<LedgerEntryType> order)
    {
        var rank = order.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);
        var ordered = charges
            .OrderBy(c => rank.TryGetValue(c.Category, out var r) ? r : int.MaxValue)
            .ThenBy(c => c.Date)
            .ToList();

        var allocations = new List<ChargeAllocation>();
        var remaining = payment;
        foreach (var c in ordered)
        {
            if (remaining <= 0m) break;
            var applied = Math.Min(remaining, c.Amount);
            if (applied <= 0m) continue;
            allocations.Add(new ChargeAllocation(c.Id, c.Category, applied));
            remaining -= applied;
        }

        return new AllocationResult(allocations, remaining);
    }

    /// <summary>
    /// Books a payment to the ledger as a single deterministic <c>Payment</c> entry linked to its
    /// audit transaction. Overpayment naturally drives the running balance negative (credit, FR-007c).
    /// </summary>
    public Task<LedgerEntry> ApplyPaymentAsync(
        Guid propertyId, Guid transactionId, decimal amount, string description,
        DateOnly? entryDate = null, string? documentNumber = null, CancellationToken ct = default) =>
        ledger.AddPaymentAsync(propertyId, transactionId, amount, description, entryDate, documentNumber, ct);
}

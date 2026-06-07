using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Statements;

/// <summary>
/// Read-only reporting over the append-only ledger (Phase 6). Produces the periodic account
/// statement (FR-039) and the statutory statement of unpaid assessments (NC § 47F-3-118). Both are
/// derived purely from <see cref="Domain.Entities.LedgerEntry"/> rows in <c>Sequence</c> order, so
/// the figures always agree with the ledger's deterministic running balance.
/// </summary>
public sealed class StatementService(ApplicationDbContext db, PaymentConfigService configService)
{
    /// <summary>
    /// Builds an account statement for <paramref name="propertyId"/> over an optional window
    /// (defaulting to the trailing 12 months through today). The opening balance is the running
    /// balance carried in from the entry immediately preceding the window; the closing balance is the
    /// last running balance within it (or the opening balance when the window is empty).
    /// </summary>
    public async Task<StatementResponse> GetStatementAsync(
        Guid propertyId, StatementRequest req, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = ParseDate(req.EndDate) ?? today;
        var start = ParseDate(req.StartDate) ?? end.AddMonths(-12);
        if (start > end) (start, end) = (end, start);

        // Opening balance = running balance of the latest entry strictly before the window.
        var openingBalance = await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId && e.EntryDate < start)
            .OrderByDescending(e => e.Sequence)
            .Select(e => (decimal?)e.RunningBalance)
            .FirstOrDefaultAsync(ct) ?? 0m;

        var lines = await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId && e.EntryDate >= start && e.EntryDate <= end)
            .OrderBy(e => e.Sequence)
            .Select(e => new StatementLineDto(
                e.EntryDate, e.DocumentNumber, e.Description,
                e.ChargeAmount, e.PaymentAmount, e.RunningBalance, e.EntryType.ToString()))
            .ToListAsync(ct);

        var totalCharges = lines.Sum(l => l.ChargeAmount);
        var totalPayments = lines.Sum(l => l.PaymentAmount);
        var closingBalance = lines.Count > 0 ? lines[^1].RunningBalance : openingBalance;

        return new StatementResponse(
            start, end, openingBalance, totalCharges, totalPayments, closingBalance, lines);
    }

    /// <summary>
    /// Produces the statutory statement of unpaid assessments (NC § 47F-3-118): the net amount owed
    /// against the lot as of today, any credit on file, and a per-category breakdown. The breakdown
    /// is computed by applying all lifetime payments to all lifetime charges in the HOA's statutory
    /// allocation order, so what remains unapplied against each category is what is still unpaid.
    /// </summary>
    public async Task<UnpaidAssessmentsResponse> GetUnpaidAssessmentsAsync(
        Guid propertyId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var entries = await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId)
            .OrderBy(e => e.Sequence)
            .Select(e => new { e.Id, e.EntryType, e.EntryDate, e.ChargeAmount, e.PaymentAmount })
            .ToListAsync(ct);

        var charges = entries
            .Where(e => e.ChargeAmount > 0m)
            .Select(e => new OpenCharge(e.Id, e.EntryType, e.EntryDate, e.ChargeAmount))
            .ToList();
        var totalPayments = entries.Sum(e => e.PaymentAmount);

        var config = await configService.GetForPropertyAsync(propertyId, ct);
        var order = AllocationService.ParseOrder(config);
        var result = AllocationService.Allocate(charges, totalPayments, order);

        // Subtract what payments covered from each charge to get the unpaid remainder per charge,
        // then roll up by category. Surplus payment (overpayment) becomes the credit balance.
        var appliedByCharge = result.Allocations
            .GroupBy(a => a.ChargeId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Applied));

        var breakdown = charges
            .GroupBy(c => c.Category)
            .Select(g => new UnpaidAssessmentLineDto(
                g.Key.ToString(),
                g.Sum(c => c.Amount - (appliedByCharge.TryGetValue(c.Id, out var applied) ? applied : 0m))))
            .Where(l => l.Amount > 0m)
            .OrderBy(l => l.Category)
            .ToList();

        var totalDue = breakdown.Sum(l => l.Amount);
        var creditBalance = result.Surplus;

        return new UnpaidAssessmentsResponse(today, totalDue, creditBalance, breakdown);
    }

    private static DateOnly? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out var d) ? d : null;
}

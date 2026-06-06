using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments;

// <!-- REPOWISE:START domain=payments -->
// Ledger, drafts, recurring payments, one-time payment (simulated ACH/card); property-scoped.
// <!-- REPOWISE:END -->

public class PaymentService(ApplicationDbContext db)
{
    private const decimal CardFee = 1.95m;

    public async Task<LedgerResponse> GetLedgerAsync(Guid propertyId, LedgerRequest req, CancellationToken ct = default)
    {
        var query = db.LedgerEntries.Where(e => e.PropertyId == propertyId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.StartDate) && DateOnly.TryParse(req.StartDate, out var start))
            query = query.Where(e => e.EntryDate >= start);

        if (!string.IsNullOrWhiteSpace(req.EndDate) && DateOnly.TryParse(req.EndDate, out var end))
            query = query.Where(e => e.EntryDate <= end);

        if (!string.IsNullOrWhiteSpace(req.Type) && Enum.TryParse<LedgerEntryType>(req.Type, true, out var type))
            query = query.Where(e => e.EntryType == type);

        if (!string.IsNullOrWhiteSpace(req.Search))
            query = query.Where(e => e.Description.Contains(req.Search) || (e.DocumentNumber != null && e.DocumentNumber.Contains(req.Search)));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.EntryDate)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(e => new LedgerItemDto(e.Id, e.EntryDate, e.DocumentNumber, e.Description, e.ChargeAmount, e.PaymentAmount, e.RunningBalance, e.EntryType.ToString()))
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(total / (double)req.PageSize);
        return new LedgerResponse(items, total, req.Page, req.PageSize, totalPages);
    }

    public async Task<RecurringPaymentDto?> GetRecurringAsync(Guid propertyId, CancellationToken ct = default)
    {
        var r = await db.RecurringPayments.FirstOrDefaultAsync(rp => rp.PropertyId == propertyId && rp.Status == "active", ct);
        return r is null ? null : MapRecurring(r);
    }

    public async Task<RecurringPaymentDto> UpsertRecurringAsync(Guid propertyId, RecurringPaymentRequest req, CancellationToken ct = default)
    {
        var existing = await db.RecurringPayments.FirstOrDefaultAsync(rp => rp.PropertyId == propertyId, ct);
        var isCard = req.Method.Equals("card", StringComparison.OrdinalIgnoreCase);

        if (existing is null)
        {
            existing = new RecurringPayment { PropertyId = propertyId };
            db.RecurringPayments.Add(existing);
        }

        existing.AmountType = Enum.Parse<RecurringAmountType>(req.AmountType, true);
        existing.FixedAmount = req.FixedAmount;
        existing.Method = isCard ? PaymentMethod.Card : PaymentMethod.Ach;
        existing.DraftDay = req.DraftDay;
        existing.Status = "active";
        existing.ProcessingFee = isCard ? CardFee : 0m;

        if (!isCard)
        {
            existing.RoutingNumberMasked = req.RoutingNumber is not null ? $"****{req.RoutingNumber[^4..]}" : null;
            existing.AccountNumberMasked = req.AccountNumber is not null ? $"****{req.AccountNumber[^4..]}" : null;
            existing.AccountType = req.AccountType;
            existing.CardNumberMasked = null;
            existing.CardExpiry = null;
            existing.CardholderName = null;
            existing.BillingZip = null;
        }
        else
        {
            existing.CardNumberMasked = req.CardNumber is not null ? $"****{req.CardNumber[^4..]}" : null;
            existing.CardExpiry = req.CardExpiry;
            existing.CardholderName = req.CardholderName;
            existing.BillingZip = req.BillingZip;
            existing.RoutingNumberMasked = null;
            existing.AccountNumberMasked = null;
            existing.AccountType = null;
        }

        await db.SaveChangesAsync(ct);
        return MapRecurring(existing);
    }

    public async Task CancelRecurringAsync(Guid propertyId, CancellationToken ct = default)
    {
        await db.RecurringPayments
            .Where(rp => rp.PropertyId == propertyId)
            .ExecuteUpdateAsync(s => s.SetProperty(rp => rp.Status, "inactive"), ct);
    }

    public async Task<IEnumerable<DraftEntryDto>> GetDraftsAsync(Guid propertyId, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddMonths(-12));
        return await db.DraftEntries
            .Where(d => d.PropertyId == propertyId && d.DraftDate >= cutoff)
            .OrderByDescending(d => d.DraftDate)
            .Select(d => new DraftEntryDto(d.Id, d.DraftDate, d.SourceLabel, d.Amount, d.Status.ToString()))
            .ToListAsync(ct);
    }

    private static RecurringPaymentDto MapRecurring(RecurringPayment r) => new(
        r.Id, r.AmountType.ToString(), r.FixedAmount, r.Method.ToString(), r.DraftDay, r.Status, r.ProcessingFee,
        r.RoutingNumberMasked, r.AccountNumberMasked, r.AccountType,
        r.CardNumberMasked, r.CardExpiry, r.CardholderName, r.BillingZip);
}

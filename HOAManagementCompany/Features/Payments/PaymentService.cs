using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Payments.Recurring;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments;

// <!-- REPOWISE:START domain=payments -->
// Ledger, drafts, recurring auto-pay (Stripe-vaulted methods + immutable mandates); property-scoped.
// <!-- REPOWISE:END -->

public class PaymentService(
    ApplicationDbContext db,
    IStripeGateway gateway,
    RecurringDraftService draftService)
{
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
        var r = await db.RecurringPayments
            .Include(x => x.Authorizations)
            .FirstOrDefaultAsync(rp => rp.PropertyId == propertyId && rp.Status == "active", ct);
        return r is null ? null : await MapRecurringAsync(r, ct);
    }

    /// <summary>
    /// Creates or updates the property's auto-pay mandate (US2, FR-009/FR-011b). The browser has
    /// already vaulted the method via a SetupIntent; we resolve the <c>pm_…</c> reference from Stripe,
    /// persist only the reference plus masked display detail (SC-001/SC-008), and append an immutable
    /// mandate authorization. No raw card/bank number is ever accepted or stored.
    /// </summary>
    public async Task<RecurringPaymentDto> UpsertRecurringAsync(
        Guid propertyId, RecurringPaymentRequest req, string? acceptedIp, string? userAgent, CancellationToken ct = default)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId, ct)
            ?? throw new InvalidOperationException($"No owner found for property {propertyId}.");

        // Ensure a Stripe customer anchors the vaulted method, persisting the id for off-session draws.
        var customerId = await gateway.EnsureCustomerAsync(
            owner.StripeCustomerId, owner.Email, $"{owner.FirstName} {owner.LastName}".Trim(), ct);
        owner.StripeCustomerId = customerId;

        var vaulted = await gateway.GetSetupIntentResultAsync(req.SetupIntentId, ct);
        var method = vaulted.PaymentMethodType == "card" ? PaymentMethod.Card : PaymentMethod.Ach;

        var existing = await db.RecurringPayments
            .Include(x => x.Authorizations)
            .FirstOrDefaultAsync(rp => rp.PropertyId == propertyId, ct);

        if (existing is null)
        {
            existing = new RecurringPayment { Id = Guid.NewGuid(), PropertyId = propertyId };
            db.RecurringPayments.Add(existing);
        }
        else
        {
            // Re-enrolling supersedes the prior mandate — terminate it (append-only, never deleted).
            var current = existing.Authorizations.FirstOrDefault(a => a.Id == existing.CurrentAuthorizationId);
            if (current is not null) current.TerminatedAt = DateTimeOffset.UtcNow;
        }

        existing.AmountType = Enum.Parse<RecurringAmountType>(req.AmountType, true);
        existing.FixedAmount = req.FixedAmount;
        existing.Method = method;
        existing.DraftDay = req.DraftDay;
        existing.Status = "active";
        existing.VaultedPaymentMethodId = vaulted.PaymentMethodId;
        existing.MethodBrand = vaulted.Brand;
        existing.MethodLast4 = vaulted.Last4;
        existing.MethodFunding = vaulted.CardFunding;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var preview = await draftService.PreviewAsync(existing, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        existing.ProcessingFee = preview.Fee;

        var auth = new PaymentAuthorization
        {
            Id = Guid.NewGuid(),
            RecurringPaymentId = existing.Id,
            MandateText = req.MandateText ?? DefaultMandateText(existing),
            MandateVersion = req.MandateVersion ?? "1.0",
            AmountTermsSnapshot = AmountTerms(existing),
            AcceptedAt = DateTimeOffset.UtcNow,
            AcceptedIp = acceptedIp,
            AcceptedUserAgent = userAgent,
            StripeMandateId = vaulted.MandateId,
        };
        db.PaymentAuthorizations.Add(auth);
        existing.CurrentAuthorizationId = auth.Id;
        existing.Authorizations.Add(auth);

        await db.SaveChangesAsync(ct);
        return await MapRecurringAsync(existing, ct);
    }

    public async Task CancelRecurringAsync(Guid propertyId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recurring = await db.RecurringPayments
            .Include(r => r.Authorizations)
            .FirstOrDefaultAsync(r => r.PropertyId == propertyId, ct);
        if (recurring is null) return;

        recurring.Status = "inactive";
        recurring.UpdatedAt = now;
        var current = recurring.Authorizations.FirstOrDefault(a => a.Id == recurring.CurrentAuthorizationId);
        if (current is not null) current.TerminatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Last-12-months drafts, newest first, with limit/offset pagination (T065). Each row carries
    /// the linked <c>PaymentTransaction</c> status when a charge has been attempted — that is the
    /// authoritative settlement state, independent of the draft's own scheduled/paid/failed flag.
    /// </summary>
    public async Task<DraftsResponse> GetDraftsAsync(Guid propertyId, DraftsRequest req, CancellationToken ct = default)
    {
        var limit = Math.Clamp(req.Limit, 1, 200);
        var offset = Math.Max(req.Offset, 0);
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddMonths(-12));

        var query = db.DraftEntries
            .Where(d => d.PropertyId == propertyId && d.DraftDate >= cutoff);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.DraftDate)
            .ThenByDescending(d => d.Id)
            .Skip(offset)
            .Take(limit)
            .Select(d => new DraftEntryDto(
                d.Id, d.DraftDate, d.SourceLabel, d.Amount, d.Status.ToString(),
                d.Transaction != null ? d.Transaction.Status.ToString() : null))
            .ToListAsync(ct);

        return new DraftsResponse(items, total, limit, offset);
    }

    private async Task<RecurringPaymentDto> MapRecurringAsync(RecurringPayment r, CancellationToken ct)
    {
        var preview = await draftService.PreviewAsync(r, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        var brand = string.IsNullOrEmpty(r.MethodBrand)
            ? (r.Method == PaymentMethod.Card ? "Card" : "Bank")
            : char.ToUpperInvariant(r.MethodBrand[0]) + r.MethodBrand[1..];
        var masked = r.MethodLast4 is not null ? $"{brand} ····{r.MethodLast4}" : null;
        var mandateAcceptedAt = r.Authorizations
            .FirstOrDefault(a => a.Id == r.CurrentAuthorizationId)?.AcceptedAt;

        return new RecurringPaymentDto(
            r.Id, r.AmountType.ToString(), r.FixedAmount, r.Method.ToString(), r.DraftDay, r.Status,
            r.ProcessingFee, masked, preview.NextDraftDate, preview.Total, mandateAcceptedAt);
    }

    private static string AmountTerms(RecurringPayment r) => r.AmountType switch
    {
        RecurringAmountType.Fixed => $"Fixed {r.FixedAmount:C} on day {r.DraftDay} of each month",
        RecurringAmountType.Assessment => $"Monthly assessment on day {r.DraftDay} of each month",
        RecurringAmountType.Balance => $"Statement balance on day {r.DraftDay} of each month",
        _ => $"Recurring draft on day {r.DraftDay} of each month",
    };

    private static string DefaultMandateText(RecurringPayment r) =>
        $"I authorize recurring payments of my {AmountTerms(r).ToLowerInvariant()} until I cancel.";
}

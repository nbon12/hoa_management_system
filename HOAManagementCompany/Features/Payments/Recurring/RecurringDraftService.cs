using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HOAManagementCompany.Features.Payments.Recurring;

/// <summary>
/// The amount owed for a draft plus the resident-facing fee, resolved at preview/charge time.
/// </summary>
public readonly record struct DraftPreview(decimal Gross, decimal Fee, decimal Total, DateOnly NextDraftDate);

/// <summary>
/// Drives the recurring auto-pay sweep (FR-010): resolves each active mandate's amount, charges the
/// vaulted method off-session, and posts the result to the append-only ledger. Per-period
/// idempotency (one charge per recurring per month) is enforced via the
/// <see cref="PaymentTransaction.IdempotencyKey"/> filtered-unique index, so a re-run of the sweep
/// is a no-op for periods already drafted.
/// </summary>
public sealed class RecurringDraftService(
    ApplicationDbContext db,
    IStripeGateway gateway,
    FeeCalculator feeCalculator,
    PaymentConfigService configService,
    LedgerService ledger,
    ILogger<RecurringDraftService> logger)
{
    /// <summary>
    /// Resolves the gross amount due for one recurring mandate: a fixed amount, the property's
    /// monthly assessment, or the current statement balance (never negative — a credit drafts $0).
    /// </summary>
    public async Task<decimal> ResolveGrossAsync(RecurringPayment r, CancellationToken ct = default)
    {
        switch (r.AmountType)
        {
            case RecurringAmountType.Fixed:
                return r.FixedAmount.GetValueOrDefault();
            case RecurringAmountType.Assessment:
                return await db.Properties
                    .Where(p => p.Id == r.PropertyId)
                    .Select(p => p.MonthlyAssessment)
                    .FirstOrDefaultAsync(ct);
            case RecurringAmountType.Balance:
                var balance = await ledger.GetCurrentBalanceAsync(r.PropertyId, ct);
                return balance > 0m ? balance : 0m;
            default:
                return 0m;
        }
    }

    /// <summary>Previews the next draft: amount due, fee, total, and the next draft date.</summary>
    public async Task<DraftPreview> PreviewAsync(RecurringPayment r, DateOnly today, CancellationToken ct = default)
    {
        var gross = await ResolveGrossAsync(r, ct);
        var config = await configService.GetForPropertyAsync(r.PropertyId, ct);
        var fee = feeCalculator.Calculate(gross, r.Method, r.MethodFunding, config);
        return new DraftPreview(fee.Gross, fee.Fee, fee.Total, NextDraftDate(r.DraftDay, today));
    }

    /// <summary>
    /// The next calendar date on which a mandate with <paramref name="draftDay"/> drafts, on or
    /// after <paramref name="today"/>. The day is clamped to the number of days in the target month
    /// (e.g. day 31 in February drafts on the 28th/29th).
    /// </summary>
    public static DateOnly NextDraftDate(int draftDay, DateOnly today)
    {
        var dayThisMonth = Math.Min(draftDay, DateTime.DaysInMonth(today.Year, today.Month));
        var candidate = new DateOnly(today.Year, today.Month, dayThisMonth);
        if (candidate >= today) return candidate;

        var nextMonth = today.AddMonths(1);
        var dayNextMonth = Math.Min(draftDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        return new DateOnly(nextMonth.Year, nextMonth.Month, dayNextMonth);
    }

    /// <summary>
    /// Charges every active mandate whose draft day matches <paramref name="asOf"/>, skipping any
    /// that have already drafted this period or have nothing due. Each charge is recorded as an
    /// immutable <see cref="PaymentTransaction"/> and, on success, posted to the ledger inside its
    /// own retrying transaction so one failed mandate never rolls back the others.
    /// </summary>
    public async Task<RunDraftsResponse> RunDueDraftsAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var due = await db.RecurringPayments
            .Where(r => r.Status == "active"
                && r.DraftDay == asOf.Day
                && r.VaultedPaymentMethodId != null)
            .ToListAsync(ct);

        int charged = 0, failed = 0, skipped = 0;

        foreach (var r in due)
        {
            var periodKey = $"draft:{r.Id:N}:{asOf:yyyy-MM}";

            if (await db.PaymentTransactions.AnyAsync(t => t.IdempotencyKey == periodKey, ct))
            {
                skipped++;
                continue;
            }

            var gross = await ResolveGrossAsync(r, ct);
            if (gross <= 0m)
            {
                // Nothing owed this period (e.g. balance fully paid / in credit) — not a charge.
                skipped++;
                continue;
            }

            var owner = await db.Owners
                .Where(o => o.PropertyId == r.PropertyId)
                .Select(o => new { o.Id, o.StripeCustomerId })
                .FirstOrDefaultAsync(ct);

            if (owner is null || string.IsNullOrWhiteSpace(owner.StripeCustomerId))
            {
                logger.LogWarning("Recurring {RecurringId} has no vaulted Stripe customer; skipping draft.", r.Id);
                failed++;
                continue;
            }

            var config = await configService.GetForPropertyAsync(r.PropertyId, ct);
            var fee = feeCalculator.Calculate(gross, r.Method, r.MethodFunding, config);
            var amountCents = (long)Math.Round(fee.Total * 100m, MidpointRounding.AwayFromZero);

            var result = await gateway.ChargeOffSessionAsync(new CreateOffSessionChargeRequest(
                owner.StripeCustomerId!,
                r.VaultedPaymentMethodId!,
                amountCents,
                "usd",
                new Dictionary<string, string>
                {
                    ["propertyId"] = r.PropertyId.ToString(),
                    ["recurringId"] = r.Id.ToString(),
                    ["period"] = asOf.ToString("yyyy-MM"),
                    ["grossAmount"] = fee.Gross.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["feeAmount"] = fee.Fee.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                IdempotencyKey: periodKey), ct);

            var succeeded = result.Status == "succeeded";

            var txn = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                PropertyId = r.PropertyId,
                OwnerId = owner.Id,
                StripePaymentIntentId = result.Id,
                StripeChargeId = result.LatestChargeId,
                GrossAmount = fee.Gross,
                FeeAmount = fee.Fee,
                Total = fee.Total,
                Currency = result.Currency,
                Status = succeeded ? TransactionStatus.Succeeded : TransactionStatus.Failed,
                PaymentMethod = r.Method,
                CardFunding = result.CardFunding ?? r.MethodFunding,
                FailureCode = result.FailureCode,
                FailureMessage = result.FailureMessage,
                IsRecurring = true,
                IdempotencyKey = periodKey,
            };

            var draft = new DraftEntry
            {
                Id = Guid.NewGuid(),
                PropertyId = r.PropertyId,
                DraftDate = asOf,
                SourceLabel = $"Auto-Pay – {(r.Method == PaymentMethod.Card ? "Card" : "ACH")}",
                Amount = fee.Total,
                Status = succeeded ? DraftStatus.Paid : DraftStatus.Failed,
                TransactionId = txn.Id,
            };

            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                db.PaymentTransactions.Add(txn);
                db.DraftEntries.Add(draft);
                await db.SaveChangesAsync(ct);

                if (succeeded)
                    await ledger.AddPaymentAsync(r.PropertyId, txn.Id, fee.Gross,
                        $"Auto-Pay – {(r.Method == PaymentMethod.Card ? "Card" : "ACH")} – {txn.StripeChargeId}",
                        entryDate: asOf, ct: ct);

                await tx.CommitAsync(ct);
            });

            if (succeeded) charged++; else failed++;
        }

        logger.LogInformation("Run-drafts {AsOf:yyyy-MM-dd}: due={Due} charged={Charged} failed={Failed} skipped={Skipped}",
            asOf, due.Count, charged, failed, skipped);

        return new RunDraftsResponse(due.Count, charged, failed, skipped);
    }
}

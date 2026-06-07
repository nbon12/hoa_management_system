using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HOAManagementCompany.Features.Payments.Recurring;

/// <summary>
/// Sends NACHA variable-amount advance notices (FR-011c). For open-balance ("Whatever I owe")
/// auto-pay, where the drafted amount can change between cycles, the resident must be told the
/// upcoming amount a configurable lead time before the draft. Runs daily from the run-drafts
/// sweep: any Balance mandate whose next draft is exactly <c>VariableNoticeLeadDays</c> away gets
/// a notice enqueued to the transactional outbox. Idempotent per mandate/period via
/// <see cref="OutboxMessage.DedupKey"/>, so a same-day re-run never double-notifies. Fixed and
/// standard-assessment schedules are excluded — their amount does not vary.
/// </summary>
public sealed class VariableNoticeService(
    ApplicationDbContext db,
    RecurringDraftService draftService,
    PaymentConfigService configService,
    ILogger<VariableNoticeService> logger)
{
    /// <summary>
    /// Enqueues advance notices for every variable-amount mandate drafting <c>leadDays</c> from
    /// <paramref name="asOf"/>. Returns the number of notice rows enqueued (one per opted-in channel).
    /// </summary>
    public async Task<int> SendDueNoticesAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var mandates = await db.RecurringPayments
            .Where(r => r.Status == "active"
                && r.AmountType == RecurringAmountType.Balance
                && r.VaultedPaymentMethodId != null)
            .ToListAsync(ct);

        var enqueued = 0;

        foreach (var r in mandates)
        {
            var config = await configService.GetForPropertyAsync(r.PropertyId, ct);
            var leadDays = config.VariableNoticeLeadDays;

            // The next draft strictly after today; notice fires exactly leadDays ahead of it.
            var draftDate = RecurringDraftService.NextDraftDate(r.DraftDay, asOf.AddDays(1));
            if (draftDate.DayNumber - asOf.DayNumber != leadDays)
                continue;

            var preview = await draftService.PreviewAsync(r, asOf, ct);
            if (preview.Total <= 0m)
                continue; // Nothing will be drafted (paid-up / in credit) — no notice required.

            var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == r.PropertyId, ct);
            if (owner is null) continue;

            var period = draftDate.ToString("yyyy-MM");
            var copy = AlertContent.VariableAmountNotice(preview.Total, draftDate);

            // Email is a transactional compliance notice — sent whenever an address is on file.
            if (!string.IsNullOrWhiteSpace(owner.Email))
                enqueued += await EnqueueAsync(owner, r, "variable_notice_email", period,
                    new AlertMessage(owner.Email, copy.EmailSubject, copy.EmailBody), ct);

            // SMS still requires TCPA opt-in plus a number on file.
            if (owner.AlertSmsOptIn && !string.IsNullOrWhiteSpace(owner.AlertPhone))
                enqueued += await EnqueueAsync(owner, r, "variable_notice_sms", period,
                    new AlertMessage(owner.AlertPhone!, null, copy.Sms), ct);
        }

        if (enqueued > 0)
            logger.LogInformation("Enqueued {Count} variable-amount notice(s) for {AsOf:yyyy-MM-dd}", enqueued, asOf);

        return enqueued;
    }

    private async Task<int> EnqueueAsync(
        Owner owner, RecurringPayment r, string kind, string period, AlertMessage payload, CancellationToken ct)
    {
        var dedupKey = $"varnotice:{r.Id:N}:{period}:{kind}";
        if (await db.OutboxMessages.AnyAsync(m => m.DedupKey == dedupKey, ct))
            return 0;

        db.OutboxMessages.Add(new OutboxMessage
        {
            Kind = kind,
            OwnerId = owner.Id,
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = OutboxStatus.Pending,
            DedupKey = dedupKey,
        });
        await db.SaveChangesAsync(ct);
        return 1;
    }
}

using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Alerts;

/// <summary>
/// Enqueues failure alerts according to the owner's opt-in matrix (FR-013, FR-015). Rows are added
/// to the change tracker but NOT saved — the caller's <c>SaveChangesAsync</c> commits them in the
/// same transaction as the status change that triggered them (transactional outbox, FR-034).
/// Alerts default OFF; an owner with no opt-in (or an opted-in SMS owner with no phone) gets none.
/// </summary>
public sealed class AlertService(ApplicationDbContext db)
{
    /// <summary>
    /// Builds masked alert rows for a failed/returned transaction. Returns the number enqueued
    /// (0 when the owner is unknown or has opted out of every channel).
    /// </summary>
    public async Task<int> EnqueueFailureAlertAsync(PaymentTransaction txn, string? code, CancellationToken ct = default)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == txn.OwnerId, ct);
        if (owner is null) return 0;

        var reason = AlertContent.FriendlyReason(code);
        var copy = AlertContent.PaymentFailed(txn, reason);
        var enqueued = 0;

        if (owner.AlertSmsOptIn && !string.IsNullOrWhiteSpace(owner.AlertPhone))
        {
            db.OutboxMessages.Add(Build(owner, txn, "sms_alert",
                new AlertMessage(owner.AlertPhone!, null, copy.Sms)));
            enqueued++;
        }

        if (owner.AlertEmailOptIn && !string.IsNullOrWhiteSpace(owner.Email))
        {
            db.OutboxMessages.Add(Build(owner, txn, "email_alert",
                new AlertMessage(owner.Email, copy.EmailSubject, copy.EmailBody)));
            enqueued++;
        }

        return enqueued;
    }

    private static OutboxMessage Build(Owner owner, PaymentTransaction txn, string kind, AlertMessage payload) =>
        new()
        {
            Kind = kind,
            OwnerId = owner.Id,
            TransactionId = txn.Id,
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = OutboxStatus.Pending,
        };
}

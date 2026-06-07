using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.Alerts;

/// <summary>
/// Drains the transactional outbox (FR-034): promptly after a webhook ack (in-process, SC-006 ≤5 min)
/// and again from the reconcile sweep as a backstop. Each row is attempted once — a provider
/// rejection is terminal (<see cref="OutboxStatus.Failed"/>, no retry, FR-019) so a hard-bouncing
/// target can never wedge the queue. Every attempt records the <c>alert.sent</c> metric.
/// </summary>
public sealed class OutboxDispatcher(
    ApplicationDbContext db,
    IEnumerable<IAlertProvider> providers,
    PaymentMetrics metrics,
    ILogger<OutboxDispatcher> logger)
{
    private const int BatchSize = 100;

    /// <summary>Dispatches up to <see cref="BatchSize"/> pending rows. Returns the count delivered.</summary>
    public async Task<int> DispatchPendingAsync(CancellationToken ct = default)
    {
        var pending = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        var delivered = 0;
        foreach (var msg in pending)
            if (await DispatchOneAsync(msg, ct))
                delivered++;

        return delivered;
    }

    private async Task<bool> DispatchOneAsync(OutboxMessage msg, CancellationToken ct)
    {
        msg.Attempts++;
        var channel = ChannelForKind(msg.Kind);

        var result = await AttemptAsync(msg, channel, ct);

        if (result.Success)
        {
            msg.Status = OutboxStatus.Sent;
            msg.SentAt = DateTimeOffset.UtcNow;
            msg.LastError = null;
        }
        else
        {
            // Terminal — never retried (FR-019), so a hard-bouncing target can't wedge the queue.
            msg.Status = OutboxStatus.Failed;
            msg.LastError = Truncate(result.Error ?? "send rejected");
            logger.LogWarning("Outbox message {Id} ({Kind}) failed: {Error}", msg.Id, msg.Kind, msg.LastError);
        }

        metrics.RecordAlertSent(channel, result.Success);
        await db.SaveChangesAsync(ct);
        return result.Success;
    }

    private async Task<AlertSendResult> AttemptAsync(OutboxMessage msg, string channel, CancellationToken ct)
    {
        var provider = providers.FirstOrDefault(p => p.Channel == channel);
        if (provider is null || !provider.IsConfigured)
            return AlertSendResult.Fail($"No configured provider for channel '{channel}'.");

        AlertMessage payload;
        try
        {
            payload = JsonSerializer.Deserialize<AlertMessage>(msg.PayloadJson)
                      ?? throw new JsonException("empty payload");
        }
        catch (Exception ex)
        {
            return AlertSendResult.Fail($"Malformed payload: {ex.Message}");
        }

        try
        {
            return await provider.SendAsync(payload, ct);
        }
        catch (Exception ex)
        {
            return AlertSendResult.Fail(ex.Message);
        }
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;

    // OutboxMessage.Kind is "<channel>_alert" / "receipt_email"; the channel half selects the provider.
    private static string ChannelForKind(string kind) => kind switch
    {
        "sms_alert" => "sms",
        "email_alert" or "receipt_email" => "email",
        _ => "email",
    };
}

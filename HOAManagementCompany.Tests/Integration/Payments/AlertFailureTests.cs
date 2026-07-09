using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Features.Payments.Webhooks;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HOAManagementCompany.Infrastructure.Payments;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// US3 failure-driven alerts (FR-014c, FR-015, FR-019): a recurring failure or an ACH return enqueues
/// alerts for opted-in owners; a one-time failure never does; and a provider rejection is terminal —
/// the row is marked Failed and never retried, so a hard-bouncing target can't wedge the queue.
/// </summary>
public class AlertFailureTests(TestDatabaseFixture fixture) : AlertTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static PaymentProviderEvent FailedEvent(string intentId, string code) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.PaymentFailed, "payment_intent.payment_failed",
        PaymentIntentId: intentId, FailureCode: code, FailureMessage: "declined");

    private async Task<PaymentTransaction> ArrangeAsync(
        ApplicationDbContext db, bool sms, bool email, string? phone,
        TransactionStatus status, PaymentMethod method, bool isRecurring)
    {
        await db.OutboxMessages.ExecuteDeleteAsync();
        var owner = await db.Owners.FirstAsync(o => o.PropertyId == PropertyId);
        owner.AlertSmsOptIn = sms;
        owner.AlertEmailOptIn = email;
        owner.AlertPhone = phone;

        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = PropertyId,
            OwnerId = owner.Id,
            StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            StripeChargeId = $"ch_test_{Guid.NewGuid():N}",
            GrossAmount = 250m,
            Total = 250m,
            Status = status,
            PaymentMethod = method,
            IsRecurring = isRecurring,
        };
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    [Fact]
    public async Task RecurringCardFailure_OptedIn_EnqueuesThenDispatches()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        var txn = await ArrangeAsync(db, sms: true, email: true, phone: "+19195550123",
            TransactionStatus.Pending, PaymentMethod.Card, isRecurring: true);

        await processor.ProcessAsync(FailedEvent(txn.StripePaymentIntentId!, "card_declined"));

        // The processor only enqueues (Pending); the dispatcher delivers (prompt path mirrors the webhook).
        var pending = await db.OutboxMessages.Where(m => m.TransactionId == txn.Id).ToListAsync();
        Assert.Equal(2, pending.Count);
        Assert.All(pending, m => Assert.Equal(OutboxStatus.Pending, m.Status));

        var delivered = await dispatcher.DispatchPendingAsync();
        Assert.Equal(2, delivered);
        Assert.Single(Sms.Sent);
        Assert.Single(Email.Sent);
        var sent = await db.OutboxMessages.Where(m => m.TransactionId == txn.Id).ToListAsync();
        Assert.All(sent, m => Assert.Equal(OutboxStatus.Sent, m.Status));
    }

    [Fact]
    public async Task OneTimeCardFailure_OptedIn_DoesNotAlert()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await ArrangeAsync(db, sms: true, email: true, phone: "+19195550123",
            TransactionStatus.Pending, PaymentMethod.Card, isRecurring: false);

        await processor.ProcessAsync(FailedEvent(txn.StripePaymentIntentId!, "card_declined"));

        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Failed, txn.Status);
        Assert.Empty(await db.OutboxMessages.Where(m => m.TransactionId == txn.Id).ToListAsync());
    }

    [Fact]
    public async Task AchReturn_OptedIn_Enqueues()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var txn = await ArrangeAsync(db, sms: false, email: true, phone: null,
            TransactionStatus.Succeeded, PaymentMethod.Ach, isRecurring: true);

        await processor.ProcessAsync(FailedEvent(txn.StripePaymentIntentId!, "R01"));

        await db.Entry(txn).ReloadAsync();
        Assert.Equal(TransactionStatus.Returned, txn.Status);
        var rows = await db.OutboxMessages.Where(m => m.TransactionId == txn.Id).ToListAsync();
        Assert.Single(rows);
        Assert.Equal("email_alert", rows[0].Kind);
    }

    [Fact]
    public async Task ProviderRejection_IsTerminal_NotRetried()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        Sms.RejectSends = true;
        var txn = await ArrangeAsync(db, sms: true, email: false, phone: "+19195550123",
            TransactionStatus.Pending, PaymentMethod.Card, isRecurring: true);

        await processor.ProcessAsync(FailedEvent(txn.StripePaymentIntentId!, "card_declined"));

        var delivered = await dispatcher.DispatchPendingAsync();
        Assert.Equal(0, delivered);
        var row = await db.OutboxMessages.SingleAsync(m => m.TransactionId == txn.Id);
        Assert.Equal(OutboxStatus.Failed, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Empty(Sms.Sent);

        // A second sweep must NOT touch the terminal row (no retry) — attempts stay at 1.
        await dispatcher.DispatchPendingAsync();
        await db.Entry(row).ReloadAsync();
        Assert.Equal(OutboxStatus.Failed, row.Status);
        Assert.Equal(1, row.Attempts);
    }
}

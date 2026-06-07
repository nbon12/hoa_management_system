using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// Reconciliation/dead-letter hardening (T083, FR-032/FR-033/FR-034). Beyond the happy-path retry
/// covered elsewhere, this proves the durability guarantees: a missed ACH settlement webhook is
/// backfilled into the ledger from Stripe; the reconcile sweep flushes outbox alerts the prompt
/// in-process dispatch missed; and an inbox event that keeps failing is dead-lettered after the
/// attempt ceiling and never retried again (so a poison event can't wedge the queue).
/// </summary>
public class ReconciliationTests(TestDatabaseFixture fixture) : AlertTestBase(fixture)
{
    private const string SchedulerSecret = "test-scheduler-secret";
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private async Task<JsonElement> ReconcileAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/jobs/reconcile");
        request.Headers.Add("X-Scheduler-Secret", SchedulerSecret);
        var res = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Reconcile_BackfillsMissedAchSettlement_IntoLedger()
    {
        // An ACH charge stuck Pending past the window because its succeeded webhook never arrived.
        Guid txnId;
        string intentId = $"pi_test_{Guid.NewGuid():N}";
        var chargeId = $"ch_test_{Guid.NewGuid():N}";
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ownerId = await db.Owners.Where(o => o.PropertyId == PropertyId).Select(o => o.Id).FirstAsync();
            var txn = new PaymentTransaction
            {
                Id = Guid.NewGuid(), PropertyId = PropertyId, OwnerId = ownerId,
                StripePaymentIntentId = intentId, GrossAmount = 175m, Total = 175m,
                Status = TransactionStatus.Pending, PaymentMethod = PaymentMethod.Ach,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
            };
            db.PaymentTransactions.Add(txn);
            await db.SaveChangesAsync();
            txnId = txn.Id;

            // Stripe (system of record) reports the intent as long since succeeded.
            Stripe.SetOutcome(intentId, new StripePaymentIntentResult(
                intentId, "secret", "succeeded", 17500, "usd", "us_bank_account", chargeId, null, null, null));
        }

        var body = await ReconcileAsync();
        Assert.True(body.GetProperty("resolvedAchTransactions").GetInt32() >= 1);

        using var verify = Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settled = await vdb.PaymentTransactions.FirstAsync(t => t.Id == txnId);
        Assert.Equal(TransactionStatus.Succeeded, settled.Status);

        // The financial record is restored, not just the status: exactly one ledger payment is posted.
        var ledgerRows = await vdb.LedgerEntries.Where(e => e.TransactionId == txnId).ToListAsync();
        var payment = Assert.Single(ledgerRows);
        Assert.Equal(175m, payment.PaymentAmount);
        Assert.Equal(LedgerEntryType.Payment, payment.EntryType);
    }

    [Fact]
    public async Task Reconcile_FlushesPendingOutboxAlert()
    {
        Guid messageId;
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ownerId = await db.Owners.Where(o => o.PropertyId == PropertyId).Select(o => o.Id).FirstAsync();
            var msg = new OutboxMessage
            {
                Id = Guid.NewGuid(), Kind = "receipt_email", OwnerId = ownerId,
                Status = OutboxStatus.Pending,
                PayloadJson = JsonSerializer.Serialize(
                    new AlertMessage("backfill@nekohoa.dev", "NekoHOA: receipt", "Your payment was received.")),
            };
            db.OutboxMessages.Add(msg);
            await db.SaveChangesAsync();
            messageId = msg.Id;
        }

        var body = await ReconcileAsync();
        Assert.True(body.GetProperty("dispatchedAlerts").GetInt32() >= 1);

        using var verify = Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var flushed = await vdb.OutboxMessages.FirstAsync(m => m.Id == messageId);
        Assert.Equal(OutboxStatus.Sent, flushed.Status);
        Assert.NotNull(flushed.SentAt);
        Assert.Contains(Email.Sent, m => m.Subject == "NekoHOA: receipt");
    }

    [Fact]
    public async Task Reconcile_DeadLettersPoisonWebhook_AfterMaxAttempts_ThenStopsRetrying()
    {
        // A poison inbox event one attempt shy of the ceiling, with a payload that always fails to parse.
        var eventId = $"evt_{Guid.NewGuid():N}";
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.WebhookEventInbox.Add(new WebhookEventInbox
            {
                Id = Guid.NewGuid(), StripeEventId = eventId, EventType = "payment_intent.succeeded",
                Status = WebhookProcessingStatus.Received, Attempts = 4, // MaxWebhookAttempts is 5.
                Payload = "<<not-a-valid-stripe-event>>",
            });
            await db.SaveChangesAsync();
        }

        await ReconcileAsync();

        using (var verify = Services.CreateScope())
        {
            var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var poison = await vdb.WebhookEventInbox.FirstAsync(w => w.StripeEventId == eventId);
            Assert.Equal(WebhookProcessingStatus.DeadLettered, poison.Status);
            Assert.Equal(5, poison.Attempts);
            Assert.False(string.IsNullOrEmpty(poison.LastError));
        }

        // A second sweep must not pick the dead-lettered event back up — attempts stay pinned at 5.
        await ReconcileAsync();

        using var verify2 = Services.CreateScope();
        var vdb2 = verify2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stillDead = await vdb2.WebhookEventInbox.FirstAsync(w => w.StripeEventId == eventId);
        Assert.Equal(WebhookProcessingStatus.DeadLettered, stillDead.Status);
        Assert.Equal(5, stillDead.Attempts);
    }
}

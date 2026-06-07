using System.Net;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>The webhook sink: signature verification, durable inbox persistence, and dedupe (FR-032/FR-017).</summary>
public class WebhookEndpointTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private async Task<HttpResponseMessage> PostWebhookAsync(string json, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/stripe")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);
        return await Client.SendAsync(request);
    }

    private async Task<PaymentTransaction> SeedPendingAchAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ownerId = await db.Owners.Where(o => o.PropertyId == PropertyId).Select(o => o.Id).FirstAsync();
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = PropertyId,
            OwnerId = ownerId,
            StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            StripeChargeId = $"ch_test_{Guid.NewGuid():N}",
            GrossAmount = 250m,
            Total = 250m,
            Status = TransactionStatus.Pending,
            PaymentMethod = PaymentMethod.Ach,
        };
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    private static string SucceededJson(string eventId, string intentId, string chargeId) =>
        JsonSerializer.Serialize(new
        {
            id = eventId,
            @object = "event",
            type = "payment_intent.succeeded",
            api_version = global::Stripe.StripeConfiguration.ApiVersion,
            request = (string?)null,   // EventConverter dereferences `request`; ParseEvent checks api_version.
            data = new { @object = new { id = intentId, @object = "payment_intent", latest_charge = chargeId } },
        });

    [Fact]
    public async Task InvalidSignature_Returns400()
    {
        var res = await PostWebhookAsync(SucceededJson($"evt_{Guid.NewGuid():N}", "pi_x", "ch_x"), "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ValidEvent_PersistsInboxAndProcesses()
    {
        var txn = await SeedPendingAchAsync();
        var eventId = $"evt_{Guid.NewGuid():N}";

        var res = await PostWebhookAsync(
            SucceededJson(eventId, txn.StripePaymentIntentId!, txn.StripeChargeId!), "t=1,v1=ok");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inbox = await db.WebhookEventInbox.FirstOrDefaultAsync(w => w.StripeEventId == eventId);
        Assert.NotNull(inbox);
        Assert.Equal(WebhookProcessingStatus.Processed, inbox!.Status);

        var reloaded = await db.PaymentTransactions.FirstAsync(t => t.Id == txn.Id);
        Assert.Equal(TransactionStatus.Succeeded, reloaded.Status);
    }

    [Fact]
    public async Task DuplicateEvent_IsAckedWithoutReprocessing()
    {
        var txn = await SeedPendingAchAsync();
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = SucceededJson(eventId, txn.StripePaymentIntentId!, txn.StripeChargeId!);

        var first = await PostWebhookAsync(json, "t=1,v1=ok");
        var second = await PostWebhookAsync(json, "t=1,v1=ok");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payments = await db.LedgerEntries
            .CountAsync(e => e.TransactionId == txn.Id && e.EntryType == LedgerEntryType.Payment);
        Assert.Equal(1, payments);   // settled exactly once despite duplicate delivery
    }
}

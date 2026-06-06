using System.Net;
using System.Net.Http.Json;
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

/// <summary>The scheduler-triggered reconciliation backstop: secret auth, stuck-ACH resolution, webhook retry (FR-033).</summary>
public class ReconcileEndpointTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private async Task<HttpResponseMessage> ReconcileAsync(string? secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/jobs/reconcile");
        if (secret is not null) request.Headers.Add("X-Scheduler-Secret", secret);
        return await Client.SendAsync(request);
    }

    [Fact]
    public async Task MissingSecret_Returns401()
    {
        var res = await ReconcileAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task WrongSecret_Returns401()
    {
        var res = await ReconcileAsync("nope");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ValidSecret_ResolvesStuckAch_AndRetriesWebhooks()
    {
        Guid stuckTxnId;
        string retryTxnIntent;
        Guid retryTxnId;

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ownerId = await db.Owners.Where(o => o.PropertyId == PropertyId).Select(o => o.Id).FirstAsync();

            // (1) A pending ACH transaction stuck past the reconcile window → Stripe says succeeded.
            var stuck = new PaymentTransaction
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
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            };

            // (2) A second pending ACH with a Received inbox event to retry.
            var retry = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                PropertyId = PropertyId,
                OwnerId = ownerId,
                StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
                StripeChargeId = $"ch_test_{Guid.NewGuid():N}",
                GrossAmount = 100m,
                Total = 100m,
                Status = TransactionStatus.Pending,
                PaymentMethod = PaymentMethod.Ach,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.PaymentTransactions.AddRange(stuck, retry);

            db.WebhookEventInbox.Add(new WebhookEventInbox
            {
                StripeEventId = $"evt_{Guid.NewGuid():N}",
                EventType = "payment_intent.succeeded",
                Status = WebhookProcessingStatus.Received,
                Payload = JsonSerializer.Serialize(new
                {
                    id = "evt_retry",
                    @object = "event",
                    type = "payment_intent.succeeded",
                    api_version = global::Stripe.StripeConfiguration.ApiVersion,
                    request = (string?)null,   // EventConverter dereferences `request`; ParseEvent checks api_version.
                    data = new
                    {
                        @object = new
                        {
                            id = retry.StripePaymentIntentId,
                            @object = "payment_intent",
                            latest_charge = retry.StripeChargeId,
                        },
                    },
                }),
            });
            await db.SaveChangesAsync();

            stuckTxnId = stuck.Id;
            retryTxnId = retry.Id;
            retryTxnIntent = retry.StripePaymentIntentId!;

            // Stripe reports the stuck intent as succeeded so the sweep settles it.
            Stripe.SetOutcome(stuck.StripePaymentIntentId!, new Infrastructure.Payments.StripePaymentIntentResult(
                stuck.StripePaymentIntentId!, "secret", "succeeded", 25000, "usd", "us_bank_account",
                stuck.StripeChargeId, null, null, null));
        }

        var res = await ReconcileAsync("test-scheduler-secret");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("resolvedAchTransactions").GetInt32() >= 1);
        Assert.True(body.GetProperty("retriedWebhooks").GetInt32() >= 1);

        using var verifyScope = Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(TransactionStatus.Succeeded,
            (await verifyDb.PaymentTransactions.FirstAsync(t => t.Id == stuckTxnId)).Status);
        Assert.Equal(TransactionStatus.Succeeded,
            (await verifyDb.PaymentTransactions.FirstAsync(t => t.Id == retryTxnId)).Status);
    }
}

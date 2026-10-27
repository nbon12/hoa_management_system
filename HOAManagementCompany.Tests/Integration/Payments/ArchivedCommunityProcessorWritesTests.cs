using System.Net;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// 030 T100 — FR-012 and spec Edge Cases "autopay mid-charge": the archive refuses people, not the
/// payment processor. Money already in flight for a home in an Archived community still settles,
/// fails, refunds and disputes, and reconciliation still corrects it; no new charge starts.
/// </summary>
public class ArchivedCommunityProcessorWritesTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task AChargeSubmittedBeforeTheArchive_Settles_AndPostsTheLedgerEntry()
    {
        var home = await ArchivedHomeAsync();
        var txn = await SeedTransactionAsync(home, TransactionStatus.Pending, PaymentMethod.Card);

        var res = await PostWebhookAsync("payment_intent.succeeded",
            new { id = txn.StripePaymentIntentId, @object = "payment_intent", latest_charge = txn.StripeChargeId });

        await AssertAcceptedAsync(res);
        Assert.Equal(TransactionStatus.Succeeded, await StatusAsync(txn.Id));
        Assert.Equal(1, await LedgerCountAsync(txn.Id, LedgerEntryType.Payment));
        Assert.True(await WithDbAsync(db => db.Receipts.AnyAsync(r => r.TransactionId == txn.Id)));
        AssertNoNewCharge();
    }

    [Fact]
    public async Task AFailedCardPayment_StillApplies()
    {
        var home = await ArchivedHomeAsync();
        var txn = await SeedTransactionAsync(home, TransactionStatus.Pending, PaymentMethod.Card);

        var res = await PostWebhookAsync("payment_intent.payment_failed", new
        {
            id = txn.StripePaymentIntentId, @object = "payment_intent",
            last_payment_error = new { code = "card_declined", message = "declined" },
        });

        await AssertAcceptedAsync(res);
        Assert.Equal(TransactionStatus.Failed, await StatusAsync(txn.Id));
        AssertNoNewCharge();
    }

    [Fact]
    public async Task AReturnedBankPayment_StillReversesTheLedger()
    {
        var home = await ArchivedHomeAsync();
        var txn = await SeedTransactionAsync(home, TransactionStatus.Succeeded, PaymentMethod.Ach);

        var res = await PostWebhookAsync("payment_intent.payment_failed", new
        {
            id = txn.StripePaymentIntentId, @object = "payment_intent",
            last_payment_error = new { code = "R01", message = "insufficient funds" },
        });

        await AssertAcceptedAsync(res);
        Assert.Equal(TransactionStatus.Returned, await StatusAsync(txn.Id));
        Assert.Equal(1, await LedgerCountAsync(txn.Id, LedgerEntryType.Reversal));
        AssertNoNewCharge();
    }

    [Fact]
    public async Task ARefund_StillApplies()
    {
        var home = await ArchivedHomeAsync();
        var txn = await SeedTransactionAsync(home, TransactionStatus.Succeeded, PaymentMethod.Card);

        var res = await PostWebhookAsync("charge.refunded",
            new { id = txn.StripeChargeId, @object = "charge", amount_refunded = 25000 });

        await AssertAcceptedAsync(res);
        Assert.Equal(TransactionStatus.Refunded, await StatusAsync(txn.Id));
        Assert.Equal(1, await LedgerCountAsync(txn.Id, LedgerEntryType.Refund));
        AssertNoNewCharge();
    }

    [Fact]
    public async Task ADispute_StillApplies()
    {
        var home = await ArchivedHomeAsync();
        var txn = await SeedTransactionAsync(home, TransactionStatus.Succeeded, PaymentMethod.Card);

        var res = await PostWebhookAsync("charge.dispute.created", new
        {
            id = $"dp_{Guid.NewGuid():N}", @object = "dispute", charge = txn.StripeChargeId, status = "warning_needs_response",
        });

        await AssertAcceptedAsync(res);
        Assert.Equal(TransactionStatus.Disputed, await StatusAsync(txn.Id));
        Assert.Equal(1, await LedgerCountAsync(txn.Id, LedgerEntryType.Chargeback));
        AssertNoNewCharge();
    }

    [Fact]
    public async Task Reconciliation_StillSettlesTheHomesStuckBankPayment()
    {
        var home = await ArchivedHomeAsync();
        var txn = await SeedTransactionAsync(home, TransactionStatus.Pending, PaymentMethod.Ach, createdAt: Clock.UtcNow.AddDays(-7));
        Stripe.SetOutcome(txn.StripePaymentIntentId!, new StripePaymentIntentResult(
            txn.StripePaymentIntentId!, "secret", "succeeded", 25000, "usd", "us_bank_account", txn.StripeChargeId, null, null, null));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/jobs/reconcile");
        request.Headers.Add("X-Scheduler-Secret", Config("Jobs:SchedulerSharedSecret"));
        var res = await CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(TransactionStatus.Succeeded, await StatusAsync(txn.Id));
        Assert.Equal(1, await LedgerCountAsync(txn.Id, LedgerEntryType.Payment));
        AssertNoNewCharge();
    }

    private async Task<Guid> ArchivedHomeAsync()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync(), CommunityStatus.Archived);
        return await CreatePropertyAsync(community);
    }

    private Task<PaymentTransaction> SeedTransactionAsync(Guid home, TransactionStatus status, PaymentMethod method,
        DateTimeOffset? createdAt = null) => WithDbAsync(async db =>
    {
        var ownerId = await db.Owners.Where(o => o.PropertyId == home).Select(o => o.Id).FirstAsync();
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(), PropertyId = home, OwnerId = ownerId, StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            StripeChargeId = $"ch_test_{Guid.NewGuid():N}", GrossAmount = 250m, FeeAmount = 0m, Total = 250m,
            Status = status, PaymentMethod = method, CreatedAt = createdAt ?? Clock.UtcNow,
        };
        db.PaymentTransactions.Add(txn);
        if (status == TransactionStatus.Succeeded)
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(), PropertyId = home, Sequence = 1, EntryDate = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime),
                Description = "Payment", PaymentAmount = 250m, RunningBalance = -250m, EntryType = LedgerEntryType.Payment,
                TransactionId = txn.Id,
            });
        await db.SaveChangesAsync();
        return txn;
    });

    // Signed the way Stripe signs it (SignedWebhookFixture), with the app's configured secret.
    private Task<HttpResponseMessage> PostWebhookAsync(string type, object dataObject)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}", @object = "event", type,
            api_version = global::Stripe.StripeConfiguration.ApiVersion, request = (string?)null,
            data = new { @object = dataObject },
        });
        var signed = new SignedWebhookFixture(json, Config("Stripe:WebhookSigningSecret"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/stripe")
        {
            Content = new StringContent(signed.Payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signed.SignatureHeader);
        return CreateClient().SendAsync(request);
    }

    private static async Task AssertAcceptedAsync(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, text);
        Assert.DoesNotContain("COMMUNITY_ARCHIVED", text);
    }

    private void AssertNoNewCharge()
    {
        Assert.Empty(Stripe.CreatedIntents);
        Assert.Empty(Stripe.OffSessionCharges);
    }

    private string Config(string key) => Services.GetRequiredService<IConfiguration>()[key]!;

    private Task<TransactionStatus> StatusAsync(Guid id) =>
        WithDbAsync(db => db.PaymentTransactions.Where(t => t.Id == id).Select(t => t.Status).SingleAsync());

    private Task<int> LedgerCountAsync(Guid transactionId, LedgerEntryType type) =>
        WithDbAsync(db => db.LedgerEntries.CountAsync(e => e.TransactionId == transactionId && e.EntryType == type));
}

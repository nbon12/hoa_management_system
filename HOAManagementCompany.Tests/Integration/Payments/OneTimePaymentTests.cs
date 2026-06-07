using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// US1 one-time payment flow over the Stripe-backed endpoints (options → intent → confirm →
/// receipt/history). Stripe is the in-memory <see cref="FakeStripeGateway"/>; no card data ever
/// reaches the backend (SC-001).
/// </summary>
public class OneTimePaymentTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Options_Unauthenticated_Returns401()
    {
        var res = await Client.GetAsync("/api/v1/payments/options");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Options_HappyPath_ReturnsFeePolicy()
    {
        await AuthenticateAsync();
        var res = await Client.GetAsync("/api/v1/payments/options");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await JsonAsync(res);
        Assert.Equal("Percentage", body.GetProperty("cardFeeType").GetString());
        Assert.Equal(0.03m, body.GetProperty("cardFeeValue").GetDecimal());
        Assert.True(body.GetProperty("surchargingEnabled").GetBoolean());
    }

    [Fact]
    public async Task CreateIntent_Card_AppliesCreditSurcharge()
    {
        await AuthenticateAsync();
        var res = await Client.PostAsJsonAsync("/api/v1/payments/intent", new { amount = 250m, method = "card" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await JsonAsync(res);
        Assert.Equal(250m, body.GetProperty("amount").GetDecimal());
        Assert.Equal(7.50m, body.GetProperty("fee").GetDecimal());     // 250 * 0.03
        Assert.Equal(257.50m, body.GetProperty("total").GetDecimal());
        Assert.StartsWith("pi_test_", body.GetProperty("paymentIntentId").GetString());
    }

    [Fact]
    public async Task CreateIntent_Ach_NoFee()
    {
        await AuthenticateAsync();
        var res = await Client.PostAsJsonAsync("/api/v1/payments/intent", new { amount = 250m, method = "ach" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await JsonAsync(res);
        Assert.Equal(0m, body.GetProperty("fee").GetDecimal());
    }

    [Theory]
    [InlineData(0, "card")]
    [InlineData(250, "wire")]
    public async Task CreateIntent_ValidationErrors_Returns422(double amount, string method)
    {
        await AuthenticateAsync();
        var res = await Client.PostAsJsonAsync("/api/v1/payments/intent", new { amount = (decimal)amount, method });
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
    }

    [Fact]
    public async Task Confirm_CardSuccess_RecordsTransactionReceiptAndHistory()
    {
        await AuthenticateAsync();
        var intent = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/intent", new { amount = 250m, method = "card" }));
        var intentId = intent.GetProperty("paymentIntentId").GetString();

        var confirmRes = await Client.PostAsJsonAsync(
            "/api/v1/payments/one-time/confirm", new { paymentIntentId = intentId });
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        var confirm = await JsonAsync(confirmRes);
        Assert.Equal("Succeeded", confirm.GetProperty("status").GetString());
        Assert.Equal(250m, confirm.GetProperty("grossAmount").GetDecimal());
        Assert.Equal("Visa •• 4242", confirm.GetProperty("maskedMethod").GetString());
        var receiptId = confirm.GetProperty("receiptId").GetString();
        Assert.False(string.IsNullOrEmpty(receiptId));

        // Receipt is retrievable.
        var receiptRes = await Client.GetAsync($"/api/v1/payments/receipts/{receiptId}");
        Assert.Equal(HttpStatusCode.OK, receiptRes.StatusCode);
        var receipt = await JsonAsync(receiptRes);
        Assert.StartsWith("CONF-", receipt.GetProperty("confirmationNumber").GetString());

        // Transaction shows up in history.
        var historyRes = await Client.GetAsync("/api/v1/payments/transactions");
        var history = await JsonAsync(historyRes);
        Assert.True(history.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Confirm_Replay_ReturnsSameTransaction()
    {
        await AuthenticateAsync();
        var intent = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/intent", new { amount = 100m, method = "card" }));
        var intentId = intent.GetProperty("paymentIntentId").GetString();

        var first = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/one-time/confirm", new { paymentIntentId = intentId }));
        var second = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/one-time/confirm", new { paymentIntentId = intentId }));

        Assert.Equal(first.GetProperty("transactionId").GetString(),
                     second.GetProperty("transactionId").GetString());
    }

    [Fact]
    public async Task Confirm_SameIdempotencyKey_CollapsesToOriginalTransaction()
    {
        await AuthenticateAsync();
        var intent = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/intent", new { amount = 175m, method = "card" }));
        var intentId = intent.GetProperty("paymentIntentId").GetString();
        var key = $"idem_{Guid.NewGuid():N}";

        async Task<JsonElement> ConfirmWithKeyAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/one-time/confirm")
            {
                Content = JsonContent.Create(new { paymentIntentId = intentId }),
            };
            request.Headers.Add(HOAManagementCompany.Features.Payments.Services.IdempotencyService.HeaderName, key);
            return await JsonAsync(await Client.SendAsync(request));
        }

        var first = await ConfirmWithKeyAsync();
        var second = await ConfirmWithKeyAsync();   // same Idempotency-Key → FindExistingAsync short-circuits

        Assert.Equal(first.GetProperty("transactionId").GetString(),
                     second.GetProperty("transactionId").GetString());
    }

    [Fact]
    public async Task Confirm_AchProcessing_StaysPending_NoReceipt()
    {
        await AuthenticateAsync();
        var intent = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/intent", new { amount = 250m, method = "ach" }));
        var intentId = intent.GetProperty("paymentIntentId").GetString()!;

        // ACH settles asynchronously: report "processing" at confirm time.
        Stripe.SetOutcome(intentId, new StripePaymentIntentResult(
            intentId, $"{intentId}_secret", "processing", 25000, "usd", "us_bank_account",
            $"ch_test_{Guid.NewGuid():N}", null, null, null, Metadata: intent_meta(intentId, 250m, 0m)));

        var confirm = await JsonAsync(await Client.PostAsJsonAsync(
            "/api/v1/payments/one-time/confirm", new { paymentIntentId = intentId }));

        Assert.Equal("Pending", confirm.GetProperty("status").GetString());
        Assert.True(confirm.GetProperty("receiptId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Confirm_MetadataPropertyMismatch_Returns403()
    {
        await AuthenticateAsync();
        var foreignIntent = $"pi_test_{Guid.NewGuid():N}";
        Stripe.SetOutcome(foreignIntent, new StripePaymentIntentResult(
            foreignIntent, $"{foreignIntent}_secret", "succeeded", 10000, "usd", "card",
            $"ch_test_{Guid.NewGuid():N}", CardFunding.Credit, "visa", "4242",
            Metadata: intent_meta(foreignIntent, 100m, 0m, propertyId: Guid.NewGuid())));

        var res = await Client.PostAsJsonAsync(
            "/api/v1/payments/one-time/confirm", new { paymentIntentId = foreignIntent });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private static Dictionary<string, string> intent_meta(string _, decimal gross, decimal fee, Guid? propertyId = null) => new()
    {
        ["propertyId"] = (propertyId ?? Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")).ToString(),
        ["grossAmount"] = gross.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["feeAmount"] = fee.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["method"] = fee == 0m ? "Ach" : "Card",
    };
}

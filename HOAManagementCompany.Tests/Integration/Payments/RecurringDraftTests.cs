using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// US2 recurring auto-pay over the Stripe-backed endpoints: vault a method via SetupIntent, enroll a
/// mandate (no raw card/bank data — SC-001), and sweep due drafts off-session. Stripe is the
/// in-memory <see cref="FakeStripeGateway"/>; no network calls in CI.
/// </summary>
public class RecurringDraftTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private const string SchedulerSecret = "test-scheduler-secret";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Enrolls a mandate, configuring a known vaulted method so off-session charges are addressable.</summary>
    private async Task<HttpResponseMessage> UpsertAsync(
        string setupIntentId, object body, StripeVaultedMethod? vaulted = null)
    {
        Stripe.SetVaultedMethod(setupIntentId, vaulted ?? new StripeVaultedMethod(
            $"pm_{setupIntentId}", "mandate_test", "card", CardFunding.Credit, "visa", "4242"));
        return await Client.PutAsJsonAsync("/api/v1/payments/recurring", body);
    }

    private HttpRequestMessage RunDrafts(string date, string? secret = SchedulerSecret)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/jobs/run-drafts?date={date}");
        if (secret is not null) req.Headers.Add("X-Scheduler-Secret", secret);
        return req;
    }

    [Fact]
    public async Task SetupIntent_HappyPath_ReturnsClientSecretAndPublishableKey()
    {
        await AuthenticateAsync();
        var res = await Client.PostAsync("/api/v1/payments/recurring/setup-intent", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await JsonAsync(res);
        Assert.StartsWith("seti_test_", body.GetProperty("setupIntentId").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("clientSecret").GetString()));
        Assert.Equal("pk_test_dummy", body.GetProperty("publishableKey").GetString());
    }

    [Fact]
    public async Task SetupIntent_Unauthenticated_Returns401()
    {
        var res = await Client.PostAsync("/api/v1/payments/recurring/setup-intent", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Upsert_HappyPath_VaultsMethodAndRecordsMandate()
    {
        await AuthenticateAsync();
        var res = await UpsertAsync("seti_happy", new
        {
            amountType = "fixed",
            fixedAmount = 100m,
            draftDay = 15,
            setupIntentId = "seti_happy",
            mandateAccepted = true,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await JsonAsync(res);
        Assert.Equal("Fixed", body.GetProperty("amountType").GetString());
        Assert.Equal("Visa ····4242", body.GetProperty("maskedMethod").GetString());
        Assert.False(body.GetProperty("mandateAcceptedAt").ValueKind == JsonValueKind.Null);
        // Fixed $100 credit card → 3% surcharge → $103 next draft.
        Assert.Equal(103m, body.GetProperty("nextDraftAmount").GetDecimal());

        // Mandate is retrievable via GET.
        var get = await JsonAsync(await Client.GetAsync("/api/v1/payments/recurring"));
        Assert.Equal("active", get.GetProperty("status").GetString());
        Assert.Equal(15, get.GetProperty("draftDay").GetInt32());
    }

    [Fact]
    public async Task Upsert_MandateNotAccepted_Returns422()
    {
        await AuthenticateAsync();
        var res = await Client.PutAsJsonAsync("/api/v1/payments/recurring", new
        {
            amountType = "assessment",
            draftDay = 1,
            setupIntentId = "seti_x",
            mandateAccepted = false,
        });
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
    }

    [Fact]
    public async Task Upsert_MissingSetupIntent_Returns422()
    {
        await AuthenticateAsync();
        var res = await Client.PutAsJsonAsync("/api/v1/payments/recurring", new
        {
            amountType = "assessment",
            draftDay = 1,
            setupIntentId = "",
            mandateAccepted = true,
        });
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
    }

    [Fact]
    public async Task Upsert_FixedWithoutAmount_Returns422()
    {
        await AuthenticateAsync();
        var res = await Client.PutAsJsonAsync("/api/v1/payments/recurring", new
        {
            amountType = "fixed",
            draftDay = 1,
            setupIntentId = "seti_x",
            mandateAccepted = true,
        });
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(0)]
    public async Task Upsert_InvalidDraftDay_Returns422(int draftDay)
    {
        await AuthenticateAsync();
        var res = await Client.PutAsJsonAsync("/api/v1/payments/recurring", new
        {
            amountType = "assessment",
            draftDay,
            setupIntentId = "seti_x",
            mandateAccepted = true,
        });
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
    }

    [Fact]
    public async Task Delete_SoftCancels_ThenGetReturns204()
    {
        await AuthenticateAsync();
        await UpsertAsync("seti_del", new
        {
            amountType = "assessment", draftDay = 1, setupIntentId = "seti_del", mandateAccepted = true,
        });

        var del = await Client.DeleteAsync("/api/v1/payments/recurring");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await Client.GetAsync("/api/v1/payments/recurring");
        Assert.Equal(HttpStatusCode.NoContent, get.StatusCode);
    }

    [Fact]
    public async Task RunDrafts_NoSecret_Returns401()
    {
        var res = await Client.SendAsync(RunDrafts("2026-07-15", secret: null));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task RunDrafts_ChargesDueMandate_PostsLedgerPayment()
    {
        await AuthenticateAsync();
        await UpsertAsync("seti_run", new
        {
            amountType = "fixed", fixedAmount = 100m, draftDay = 15,
            setupIntentId = "seti_run", mandateAccepted = true,
        });

        var run = await JsonAsync(await Client.SendAsync(RunDrafts("2026-07-15")));
        Assert.Equal(1, run.GetProperty("charged").GetInt32());
        Assert.Equal(0, run.GetProperty("failed").GetInt32());

        // The off-session charge billed gross + 3% surcharge = $103.00 (10300 cents).
        var charge = Assert.Single(Stripe.OffSessionCharges);
        Assert.Equal(10300, charge.AmountCents);
        Assert.Equal("pm_seti_run", charge.PaymentMethodId);

        // A draft entry was recorded as Paid, and surfaces its linked transaction's Succeeded status.
        var drafts = await JsonAsync(await Client.GetAsync("/api/v1/payments/drafts"));
        Assert.Contains(drafts.GetProperty("items").EnumerateArray(), d =>
            d.GetProperty("status").GetString() == "Paid" && d.GetProperty("amount").GetDecimal() == 103m
            && d.GetProperty("transactionStatus").GetString() == "Succeeded");
    }

    [Fact]
    public async Task RunDrafts_SamePeriodTwice_IsIdempotent()
    {
        await AuthenticateAsync();
        await UpsertAsync("seti_idem", new
        {
            amountType = "fixed", fixedAmount = 50m, draftDay = 15,
            setupIntentId = "seti_idem", mandateAccepted = true,
        });

        var first = await JsonAsync(await Client.SendAsync(RunDrafts("2026-08-15")));
        Assert.Equal(1, first.GetProperty("charged").GetInt32());

        var second = await JsonAsync(await Client.SendAsync(RunDrafts("2026-08-15")));
        Assert.Equal(0, second.GetProperty("charged").GetInt32());
        Assert.Equal(1, second.GetProperty("skipped").GetInt32());

        // Only one off-session charge was ever issued for the period.
        Assert.Single(Stripe.OffSessionCharges);
    }

    [Fact]
    public async Task RunDrafts_DeclinedCharge_RecordsFailureNoLedgerPayment()
    {
        await AuthenticateAsync();
        await UpsertAsync("seti_fail", new
        {
            amountType = "fixed", fixedAmount = 75m, draftDay = 15,
            setupIntentId = "seti_fail", mandateAccepted = true,
        });

        // Force the vaulted method to decline off-session.
        Stripe.SetOffSessionOutcome("pm_seti_fail", new StripePaymentIntentResult(
            "pi_failed", "pi_failed_secret", "requires_payment_method", 0, "usd", "card", null,
            CardFunding.Credit, "visa", "4242", FailureCode: "card_declined", FailureMessage: "Your card was declined."));

        var run = await JsonAsync(await Client.SendAsync(RunDrafts("2026-09-15")));
        Assert.Equal(0, run.GetProperty("charged").GetInt32());
        Assert.Equal(1, run.GetProperty("failed").GetInt32());

        var drafts = await JsonAsync(await Client.GetAsync("/api/v1/payments/drafts"));
        Assert.Contains(drafts.GetProperty("items").EnumerateArray(), d =>
            d.GetProperty("status").GetString() == "Failed"
            && d.GetProperty("transactionStatus").GetString() == "Failed");
    }

    [Fact]
    public async Task Drafts_Returns12MonthsOfEntries()
    {
        await AuthenticateAsync();
        var res = await Client.GetAsync("/api/v1/payments/drafts");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Drafts_LimitOffset_PaginatesNewestFirst()
    {
        await AuthenticateAsync();

        // Three drafts over distinct months so ordering is unambiguous.
        await UpsertAsync("seti_pg", new
        {
            amountType = "fixed", fixedAmount = 10m, draftDay = 15,
            setupIntentId = "seti_pg", mandateAccepted = true,
        });
        await Client.SendAsync(RunDrafts("2026-03-15"));
        await Client.SendAsync(RunDrafts("2026-04-15"));
        await Client.SendAsync(RunDrafts("2026-05-15"));

        var page1 = await JsonAsync(await Client.GetAsync("/api/v1/payments/drafts?limit=2&offset=0"));
        Assert.True(page1.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(2, page1.GetProperty("limit").GetInt32());
        Assert.Equal(0, page1.GetProperty("offset").GetInt32());
        var firstPage = page1.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, firstPage.Count);

        // Newest-first: the first item's draft date is not before the second's.
        var d0 = firstPage[0].GetProperty("draftDate").GetString();
        var d1 = firstPage[1].GetProperty("draftDate").GetString();
        Assert.True(string.CompareOrdinal(d0, d1) >= 0);

        // Offset advances the window — page 2 yields different rows.
        var page2 = await JsonAsync(await Client.GetAsync("/api/v1/payments/drafts?limit=2&offset=2"));
        var secondPage = page2.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(secondPage);
        Assert.DoesNotContain(secondPage, d =>
            d.GetProperty("id").GetString() == firstPage[0].GetProperty("id").GetString());
    }
}

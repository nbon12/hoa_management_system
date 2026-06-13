using System.Net;
using System.Text;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Sandbox;

/// <summary>
/// US1 (P1, MVP). Exercises the <b>real</b> <see cref="IStripeGateway"/> adapter surface and the
/// webhook signature → persistence path against Stripe test mode — no real charges (FR-011/012/013).
/// </summary>
[Trait("Category", "Sandbox")]
public class StripeSandboxTests : SandboxIntegrationTestBase
{
    public StripeSandboxTests(TestDatabaseFixture fixture) : base(fixture) { }

    private IStripeGateway Gateway => Services.GetRequiredService<IStripeGateway>();
    private string SecretKey => Services.GetRequiredService<IConfiguration>()["Stripe:SecretKey"]!;
    private string SigningSecret => Services.GetRequiredService<IConfiguration>()["Stripe:WebhookSigningSecret"]!;

    [SkippableFact]
    public async Task Adapter_surface_round_trips_against_test_mode()
    {
        RequireStripe();
        var gw = Gateway;

        // Customer (FR-011)
        var customerId = await SandboxResult.RunAsync(() =>
            gw.EnsureCustomerAsync(null, "sandbox@nekohoa.dev", "Sandbox Tester"));
        Assert.StartsWith("cus_", customerId);

        // One-time PaymentIntent + fetch
        var pi = await SandboxResult.RunAsync(() =>
            gw.CreatePaymentIntentAsync(new CreatePaymentIntentRequest(1500, "usd")));
        Assert.StartsWith("pi_", pi.Id);

        var fetched = await SandboxResult.RunAsync(() => gw.GetPaymentIntentAsync(pi.Id));
        Assert.Equal(pi.Id, fetched.Id);

        // Vault a method via SetupIntent, confirmed server-side with a canonical test PM (headless, R5).
        var si = await SandboxResult.RunAsync(() => gw.CreateSetupIntentAsync(customerId));
        Assert.StartsWith("seti_", si.Id);

        // ReturnUrl is required for a headless server-side confirm: the SetupIntent enables automatic
        // payment methods, which may include redirect-based ones, so Stripe demands a return target
        // even though pm_card_visa never redirects. Production confirms via the Payment Element, which
        // supplies its own return_url — this is a test-only concern.
        var confirmed = await SandboxResult.RunAsync(() =>
            new SetupIntentService(new StripeClient(SecretKey)).ConfirmAsync(
                si.Id, new SetupIntentConfirmOptions
                {
                    PaymentMethod = "pm_card_visa",
                    ReturnUrl = "https://sandbox.nekohoa.dev/setup-complete",
                }));
        Assert.Equal("succeeded", confirmed.Status);

        var vaulted = await SandboxResult.RunAsync(() => gw.GetSetupIntentResultAsync(si.Id));
        Assert.StartsWith("pm_", vaulted.PaymentMethodId);

        // Off-session charge against the vaulted method
        var offSession = await SandboxResult.RunAsync(() =>
            gw.ChargeOffSessionAsync(new CreateOffSessionChargeRequest(
                customerId, vaulted.PaymentMethodId, 2500, "usd")));
        Assert.Equal("succeeded", offSession.Status);

        // Settlement detail on the resulting charge
        Skip.If(offSession.LatestChargeId is null, "no charge id returned by off-session charge");
        var charge = await SandboxResult.RunAsync(() => gw.GetChargeAsync(offSession.LatestChargeId!));
        Assert.NotNull(charge);
        Assert.StartsWith("ch_", charge!.ChargeId);
    }

    [SkippableFact]
    public async Task Valid_signed_webhook_is_accepted_and_persisted()
    {
        RequireStripe();

        var eventId = $"evt_stage2_valid_{Guid.NewGuid():N}";
        var payload = BuildEventPayload(eventId);
        var signed = new SignedWebhookFixture(payload, SigningSecret);

        var resp = await PostWebhookAsync(signed);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var db = NewDbContext();
        var row = await db.WebhookEventInbox.SingleOrDefaultAsync(x => x.StripeEventId == eventId);
        Assert.NotNull(row);
        Assert.Equal("payment_intent.succeeded", row!.EventType);
    }

    [SkippableFact]
    public async Task Tampered_signature_is_rejected_and_not_persisted()
    {
        RequireStripe();

        var eventId = $"evt_stage2_tampered_{Guid.NewGuid():N}";
        var payload = BuildEventPayload(eventId);
        var signed = new SignedWebhookFixture(payload, SigningSecret, tamper: true);

        var resp = await PostWebhookAsync(signed);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        await using var db = NewDbContext();
        var row = await db.WebhookEventInbox.SingleOrDefaultAsync(x => x.StripeEventId == eventId);
        Assert.Null(row);
    }

    // Loads the captured event template and stamps a unique id (DB isolation across the two webhook
    // tests, which share the Testcontainers DB) and the SDK's pinned API version (so the real
    // ConstructEvent does not reject on an api_version mismatch). Substitution happens BEFORE signing.
    private static string BuildEventPayload(string eventId)
    {
        var path = Path.Combine(AppContext.BaseDirectory,
            "Integration", "Sandbox", "Fixtures", "stripe-payment-intent-succeeded.json");
        return System.IO.File.ReadAllText(path)
            .Replace("__EVENT_ID__", eventId)
            .Replace("__API_VERSION__", StripeConfiguration.ApiVersion);
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(SignedWebhookFixture signed)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/stripe")
        {
            Content = new StringContent(signed.Payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("Stripe-Signature", signed.SignatureHeader);
        return await Client.SendAsync(req);
    }

    private ApplicationDbContext NewDbContext() =>
        Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
}

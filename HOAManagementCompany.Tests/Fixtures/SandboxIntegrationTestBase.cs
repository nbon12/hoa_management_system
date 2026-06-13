using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Base for Stage-2 (007) provider sandbox tests. Unlike <see cref="PaymentTestBase"/> /
/// <see cref="AlertTestBase"/>, it does <b>not</b> swap <c>IStripeGateway</c> or <c>IAlertProvider</c>,
/// so the <b>real</b> adapters run against each provider's test/sandbox mode (research R1).
///
/// <para>
/// Real test-mode secrets are read from environment variables in <see cref="ExtraConfiguration"/>
/// (which wins over the harness defaults). Each provider exposes a <c>RequireX()</c> guardrail that
/// (a) <b>skips</b> the test when its secret is absent (FR-007) and (b) <b>hard-fails</b> if a
/// non-test credential is supplied, so a live key can never drive Stage 2 (FR-008/FR-009). Raw secret
/// values are never logged (FR-010).
/// </para>
/// </summary>
public abstract class SandboxIntegrationTestBase : IntegrationTestBase
{
    protected SandboxIntegrationTestBase(TestDatabaseFixture fixture) : base(fixture) { }

    // Trim env-sourced secrets: a value pasted into a CI secret store often carries a trailing
    // newline, and a Stripe key with any whitespace is rejected by the SDK ("API key cannot contain
    // whitespace"). Treat whitespace-only values as absent so the matching RequireX() skips cleanly.
    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } v ? v : null;

    /// <summary>
    /// Inject real test-mode secrets from the environment. Absent values are omitted so the matching
    /// <c>RequireX()</c> skips rather than running against a half-configured provider. <c>ApiKeySid</c>
    /// is intentionally left unset so the Twilio adapter takes its basic-auth (test-credential) path.
    /// </summary>
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = Env("Stripe__SecretKey"),
            ["Stripe:WebhookSigningSecret"] = Env("Stripe__WebhookSigningSecret"),

            ["SendGrid:ApiKey"] = Env("SendGrid__ApiKey"),
            ["SendGrid:FromEmail"] = Env("SendGrid__FromEmail"),
            ["SendGrid:FromName"] = "NekoHOA Sandbox",
            // Default sandbox ON — the sole no-deliver guardrail for email.
            ["SendGrid:Sandbox"] = Env("SendGrid__Sandbox") ?? "true",

            ["Twilio:AccountSid"] = Env("Twilio__AccountSid"),
            ["Twilio:AuthToken"] = Env("Twilio__AuthToken"),
            // Force the basic-auth path: blank out any ApiKeySid that appsettings.Secrets.json
            // (or another config source) might supply, so the adapter authenticates with
            // AccountSid + AuthToken — the only auth that honors Twilio magic numbers. The empty
            // string survives the null-filter below and overrides the inherited value.
            ["Twilio:ApiKeySid"] = "",
            // Magic From required under test credentials; non-magic throws.
            ["Twilio:FromNumber"] = Env("Twilio__FromNumber") ?? "+15005550006",
        }
        .Where(kv => kv.Value is not null)
        .ToList();

    private IConfiguration Config => Services.GetRequiredService<IConfiguration>();

    /// <summary>FR-009: require a Stripe <c>sk_test_</c>/<c>rk_test_</c> key; skip if unset, fail if live.</summary>
    protected void RequireStripe()
    {
        var key = Config["Stripe:SecretKey"];
        Skip.If(string.IsNullOrWhiteSpace(key), "Stripe test key not configured");
        if (!(key!.StartsWith("sk_test_", StringComparison.Ordinal)
              || key.StartsWith("rk_test_", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "Refusing to run Stage 2: Stripe:SecretKey is not a test key (sk_test_/rk_test_).");

        Skip.If(string.IsNullOrWhiteSpace(Config["Stripe:WebhookSigningSecret"]),
            "Stripe webhook signing secret not configured");
    }

    /// <summary>FR-009: SendGrid keys have no test/live form — sandbox mode is the only guardrail.</summary>
    protected void RequireSendGrid()
    {
        Skip.If(string.IsNullOrWhiteSpace(Config["SendGrid:ApiKey"])
                || string.IsNullOrWhiteSpace(Config["SendGrid:FromEmail"]),
            "SendGrid not configured");
        if (!Config.GetValue<bool>("SendGrid:Sandbox"))
            throw new InvalidOperationException(
                "Refusing to send: SendGrid:Sandbox must be true in Stage 2 (sole no-deliver guardrail).");
    }

    /// <summary>FR-009: Twilio test SIDs look like live SIDs, so require an explicit acknowledgement.</summary>
    protected void RequireTwilio()
    {
        var sid = Config["Twilio:AccountSid"];
        var token = Config["Twilio:AuthToken"];
        Skip.If(string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token),
            "Twilio test credentials not configured");

        var ack = Environment.GetEnvironmentVariable("TWILIO_TEST_CREDENTIALS");
        if (!sid!.StartsWith("AC", StringComparison.Ordinal)
            || !string.Equals(ack, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Refusing to run Stage 2: set a test AccountSid (AC…) and TWILIO_TEST_CREDENTIALS=true.");
    }
}

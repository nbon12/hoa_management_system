using System.Diagnostics.CodeAnalysis;
using HOAManagementCompany.Features.Payments;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// SMS delivery via Twilio (FR-016). Thin network adapter — excluded from coverage like the Stripe
/// gateway; the testable logic lives in <c>OutboxDispatcher</c>/<c>AlertService</c> behind the fake.
/// Every message carries the TCPA opt-out instruction (FR-020).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TwilioSmsProvider(IOptions<TwilioOptions> options, ILogger<TwilioSmsProvider> logger)
    : IAlertProvider
{
    private const string OptOutSuffix = " Reply STOP to unsubscribe.";
    private readonly TwilioOptions _options = options.Value;

    public string Channel => "sms";
    public bool IsConfigured => _options.IsConfigured;

    public async Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured) return AlertSendResult.Fail("Twilio is not configured.");
        try
        {
            // <!-- REPOWISE:START domain=payments-alerts -->
            // Stage 2 (007) test-credential path. Twilio honors magic numbers (test mode) only under
            // Account SID + Auth Token basic auth, not API-key auth. When no API key is configured but
            // an Auth Token is, authenticate basic so the sandbox tests can exercise the real adapter.
            // Default-off: production keeps API-key auth (ApiKeySid set), unchanged.
            if (string.IsNullOrWhiteSpace(_options.ApiKeySid) && !string.IsNullOrWhiteSpace(_options.AuthToken))
                TwilioClient.Init(_options.AccountSid, _options.AuthToken);
            else
                TwilioClient.Init(_options.ApiKeySid, _options.ApiKeySecret, _options.AccountSid);
            // <!-- REPOWISE:END -->

            var body = message.Body.Contains("STOP", StringComparison.OrdinalIgnoreCase)
                ? message.Body
                : message.Body + OptOutSuffix;
            var sent = await MessageResource.CreateAsync(
                to: new PhoneNumber(message.Target),
                from: new PhoneNumber(_options.FromNumber),
                body: body);
            return AlertSendResult.Ok(sent.Sid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Twilio SMS send failed");
            return AlertSendResult.Fail(ex.Message);
        }
    }
}

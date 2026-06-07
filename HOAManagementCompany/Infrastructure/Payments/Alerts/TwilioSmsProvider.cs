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
            TwilioClient.Init(_options.ApiKeySid, _options.ApiKeySecret, _options.AccountSid);
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

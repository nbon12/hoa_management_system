using System.Diagnostics.CodeAnalysis;
using HOAManagementCompany.Features.Payments;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// Email delivery via SendGrid (FR-016). Thin network adapter — excluded from coverage like the
/// Stripe gateway; the testable logic lives in <c>OutboxDispatcher</c>/<c>AlertService</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SendGridEmailProvider(IOptions<SendGridOptions> options, ILogger<SendGridEmailProvider> logger)
    : IAlertProvider
{
    private readonly SendGridOptions _options = options.Value;

    public string Channel => "email";
    public bool IsConfigured => _options.IsConfigured;

    public async Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured) return AlertSendResult.Fail("SendGrid is not configured.");
        try
        {
            var client = new SendGridClient(_options.ApiKey);
            var msg = MailHelper.CreateSingleEmail(
                new EmailAddress(_options.FromEmail, _options.FromName),
                new EmailAddress(message.Target),
                message.Subject ?? "NekoHOA payment alert",
                message.Body,
                message.Body);
            // <!-- REPOWISE:START domain=payments-alerts -->
            // Stage 2 (007) no-deliver seam. When Sandbox is on, SendGrid validates the request
            // (auth + payload — note it does NOT enforce sender verification in sandbox) and returns
            // 2xx WITHOUT delivering — the only no-deliver guardrail for email (keys have no test/live
            // form). Default-off: the production path leaves MailSettings unset and delivers as before.
            if (_options.Sandbox)
                msg.MailSettings = new MailSettings { SandboxMode = new SandboxMode { Enable = true } };
            // <!-- REPOWISE:END -->
            var response = await client.SendEmailAsync(msg, ct);
            var code = (int)response.StatusCode;
            return code is >= 200 and < 300
                ? AlertSendResult.Ok()
                : AlertSendResult.Fail($"SendGrid returned {code}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SendGrid email send failed");
            return AlertSendResult.Fail(ex.Message);
        }
    }
}

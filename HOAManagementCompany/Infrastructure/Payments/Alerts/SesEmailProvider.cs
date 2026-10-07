using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using HOAManagementCompany.Features.Payments;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// Email delivery via Amazon SES (SESv2) for alerts, receipts, and auth codes (026). Never throws: every failure becomes a handled <see cref="AlertSendResult.Fail"/> with a
/// secret-free reason from <see cref="SesErrorMapper"/>. The SDK client comes from
/// <see cref="ISesClientFactory"/> so the decision logic here stays unit-testable.
/// </summary>
public sealed class SesEmailProvider(
    IOptions<SesOptions> options,
    ISesClientFactory clientFactory,
    ILogger<SesEmailProvider> logger) : IAlertProvider
{
    private const string DefaultSubject = "NekoHOA payment alert";

    private readonly SesOptions _options = options.Value;
    private readonly Lazy<IAmazonSimpleEmailServiceV2> _client = new(() => clientFactory.Create(options.Value));

    public string Channel => "email";
    public bool IsConfigured => _options.IsConfigured;

    public async Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured) return AlertSendResult.Fail("SES is not configured.");
        // <!-- REPOWISE:START domain=payments-alerts -->
        // Stage 2 (026) no-deliver seam. SES keys have no test/live form, so SimulatorOnly is the sole
        // guardrail: it limits recipients to the SES mailbox simulator and refuses everything else
        // here, before any client is created or network call made. Default-off: production delivers.
        if (_options.SimulatorOnly && !SimulatorRecipientGuard.IsAllowed(message.Target))
            return AlertSendResult.Fail("SES SimulatorOnly guard refused a non-simulator recipient.");
        // <!-- REPOWISE:END -->
        try
        {
            var request = new SendEmailRequest
            {
                FromEmailAddress = $"\"{_options.FromName}\" <{_options.FromEmail}>",
                Destination = new Destination { ToAddresses = [message.Target] },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Charset = "UTF-8", Data = message.Subject ?? DefaultSubject },
                        // Plain text only: every body is plain text, and an HTML part would let
                        // interpolated values be read as markup (026 research R2).
                        Body = new Body { Text = new Content { Charset = "UTF-8", Data = message.Body } },
                    },
                },
            };
            var response = await _client.Value.SendEmailAsync(request, ct);
            var code = (int)response.HttpStatusCode;
            return code is >= 200 and < 300
                ? AlertSendResult.Ok(response.MessageId)
                : AlertSendResult.Fail($"{SesErrorMapper.RejectedPrefix} HTTP {code}");
        }
        catch (Exception ex)
        {
            // Log only the mapped reason — never the exception, whose message can carry the
            // recipient address or access key ID (026 FR-009).
            var reason = SesErrorMapper.Map(ex);
            logger.LogWarning("SES email send failed: {Reason}", reason);
            return AlertSendResult.Fail(reason);
        }
    }
}

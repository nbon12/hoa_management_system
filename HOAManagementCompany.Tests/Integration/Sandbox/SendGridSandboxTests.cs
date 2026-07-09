using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Sandbox;

/// <summary>
/// US2 (P2). Exercises the <b>real</b> SendGrid adapter in sandbox mode — SendGrid validates auth,
/// sender, and payload and returns 2xx <b>without delivering</b> (FR-015), plus the handled-failure
/// path for an unverified sender (FR-016). Zero email delivered (SC-003).
/// </summary>
[Trait("Category", "Sandbox")]
public class SendGridSandboxTests : SandboxIntegrationTestBase
{
    public SendGridSandboxTests(TestDatabaseFixture fixture) : base(fixture) { }

    private IConfiguration Config => Services.GetRequiredService<IConfiguration>();
    private IAlertProvider Email =>
        Services.GetServices<IAlertProvider>().Single(p => p.Channel == "email");

    [SkippableFact]
    public async Task Sandbox_send_is_accepted_without_delivery()
    {
        RequireSendGrid(); // asserts Sandbox == true — the sole no-deliver guardrail

        var message = new AlertMessage(
            Target: "resident@nekohoa.dev",
            Subject: "NekoHOA payment receipt (sandbox)",
            Body: "Your autopay of $25.00 was received.");

        var result = await SandboxResult.RunAsync(() => Email.SendAsync(message));

        Assert.True(result.Success, result.Error);
    }

    [SkippableFact]
    public async Task Malformed_sender_is_reported_as_a_handled_failure()
    {
        RequireSendGrid();

        // Same real key + sandbox mode, but a malformed From address. Sandbox mode validates the
        // request payload (it does NOT enforce sender *verification*), so an invalid address yields a
        // non-2xx, which the adapter maps to AlertSendResult.Fail (it never throws). Still no delivery.
        var badSender = Options.Create(new SendGridOptions
        {
            ApiKey = Config["SendGrid:ApiKey"]!,
            FromEmail = "not-a-valid-email-address",
            FromName = "Stage 2 Sandbox",
            Sandbox = true,
        });
        var provider = new SendGridEmailProvider(
            badSender, Services.GetRequiredService<ILogger<SendGridEmailProvider>>());

        var result = await SandboxResult.RunAsync(() => provider.SendAsync(
            new AlertMessage("resident@nekohoa.dev", "Subject", "Body")));

        Assert.False(result.Success);
    }
}

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
/// 026 US1/US2. Exercises the <b>real</b> SES adapter against the SES API with the mailbox simulator
/// (<c>@simulator.amazonses.com</c>), which returns real responses without delivering to a person.
/// <c>Ses:SimulatorOnly</c> refuses every other recipient before any network call. Zero email
/// delivered (SC-002).
/// </summary>
[Trait("Category", "Sandbox")]
public class SesSandboxTests : SandboxIntegrationTestBase
{
    private const string SuccessSimulator = "success@simulator.amazonses.com";
    private const string BounceSimulator = "bounce@simulator.amazonses.com";

    public SesSandboxTests(TestDatabaseFixture fixture) : base(fixture) { }

    private IConfiguration Config => Services.GetRequiredService<IConfiguration>();
    private IAlertProvider Email =>
        Services.GetServices<IAlertProvider>().Single(p => p.Channel == "email");

    private static AlertMessage Message(string target) =>
        new(target, "NekoHOA payment receipt (sandbox)", "Your autopay of $25.00 was received.");

    /// <summary>A provider using this run's real region/keys but caller-chosen sender or credentials.</summary>
    private SesEmailProvider ProviderWith(string? fromEmail = null, string? accessKeyId = null, string? secret = null) =>
        new(Options.Create(new SesOptions
            {
                Region = Config["Ses:Region"]!,
                FromEmail = fromEmail ?? Config["Ses:FromEmail"]!,
                FromName = "Stage 2 Sandbox",
                AccessKeyId = accessKeyId ?? Config["Ses:AccessKeyId"]!,
                SecretAccessKey = secret ?? Config["Ses:SecretAccessKey"]!,
                SimulatorOnly = true,
            }),
            new SesClientFactory(),
            Services.GetRequiredService<ILogger<SesEmailProvider>>());

    [SkippableFact]
    public async Task Simulator_success_send_is_accepted()
    {
        RequireSes();

        var result = await SandboxResult.RunAsync(() => Email.SendAsync(Message(SuccessSimulator)));
        SandboxResult.SkipIfUnavailable(result);

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderMessageId));
    }

    [SkippableFact]
    public async Task Malformed_sender_is_reported_as_a_handled_failure()
    {
        RequireSes();

        var result = await SandboxResult.RunAsync(() =>
            ProviderWith(fromEmail: "not-a-valid-email-address").SendAsync(Message(SuccessSimulator)));
        SandboxResult.SkipIfUnavailable(result);

        Assert.False(result.Success);
        Assert.StartsWith(SesErrorMapper.RejectedPrefix, result.Error);
    }

    [SkippableFact]
    public async Task Invalid_credentials_are_a_rejection_not_an_outage()
    {
        RequireSes();

        // No SkipIfUnavailable: bad credentials must Fail the run and block the release (US1-4).
        var result = await SandboxResult.RunAsync(() =>
            ProviderWith(accessKeyId: "invalid-access-key-id", secret: "invalid").SendAsync(Message(SuccessSimulator)));

        Assert.False(result.Success);
        Assert.StartsWith(SesErrorMapper.RejectedPrefix, result.Error);
    }

    [SkippableFact]
    public async Task Guard_refuses_real_recipient_without_calling_ses()
    {
        RequireSes();

        var result = await Email.SendAsync(Message("resident@nekohoa.dev"));

        Assert.False(result.Success);
        Assert.Contains("SimulatorOnly", result.Error);
    }

    [SkippableFact]
    public async Task Bounce_simulator_send_is_accepted()
    {
        RequireSes();

        // SES accepts the send and reports the bounce asynchronously; bounce events are out of scope.
        var result = await SandboxResult.RunAsync(() => Email.SendAsync(Message(BounceSimulator)));
        SandboxResult.SkipIfUnavailable(result);

        Assert.True(result.Success, result.Error);
    }
}

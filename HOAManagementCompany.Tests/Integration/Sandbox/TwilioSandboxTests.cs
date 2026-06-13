using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Sandbox;

/// <summary>
/// US3 (P3). Exercises the <b>real</b> Twilio adapter against test credentials and magic numbers
/// (FR-017/FR-018): the success number returns a <c>Sid</c>; the invalid number maps to a handled
/// failure. No real SMS is sent (SC-003). Requires the basic-auth (test-credential) adapter path.
/// </summary>
[Trait("Category", "Sandbox")]
public class TwilioSandboxTests : SandboxIntegrationTestBase
{
    public TwilioSandboxTests(TestDatabaseFixture fixture) : base(fixture) { }

    private IAlertProvider Sms =>
        Services.GetServices<IAlertProvider>().Single(p => p.Channel == "sms");

    /// <summary>
    /// FR-018: a magic <b>invalid</b> number (+15005550001) drives Twilio to a 21211 rejection, which
    /// the real adapter must map to <see cref="AlertSendResult.Fail"/> without throwing. This is the
    /// hard gate: a green run proves the basic-auth test-credential path authenticates, reaches Twilio,
    /// and maps a domain rejection to a handled failure. A credential/auth regression would surface
    /// here as a thrown/unexpected error rather than this clean handled failure.
    /// </summary>
    [SkippableFact]
    public async Task Magic_invalid_number_maps_to_handled_failure()
    {
        RequireTwilio();

        var result = await SandboxResult.RunAsync(() => Sms.SendAsync(
            Msg("+15005550001")));

        Assert.False(result.Success, "magic invalid number must map to a handled failure");
        Assert.False(string.IsNullOrWhiteSpace(result.Error), "a handled failure carries an error");
    }

    /// <summary>
    /// FR-017: the adapter returns a Twilio <c>Sid</c> on an accepted send. Twilio test credentials
    /// expose no policy-bypassing magic number for <i>success</i>: +15005550006 is the only all-valid
    /// number and is the required test <c>From</c>, and <c>To == From</c> is rejected. Any other (real)
    /// <c>To</c> is "validated normally", so the send can be refused by environmental account policy
    /// (A2P 10DLC for US long codes — 21606; or geo-permissions for other regions — 21408) rather than
    /// by a code defect. Per the <see cref="SandboxResult"/> philosophy (environment → skip, regression
    /// → fail) we <b>skip</b> on those provisioning rejections so a release is never blocked by Twilio
    /// account configuration; where the account permits the send, the Sid is asserted.
    /// </summary>
    [SkippableFact]
    public async Task Accepted_send_returns_a_sid()
    {
        RequireTwilio();

        var result = await SandboxResult.RunAsync(() => Sms.SendAsync(
            Msg("+12128675309"))); // valid US (+1) NANP number; "validated normally", no real SMS sent

        Skip.If(!result.Success && IsAccountProvisioningRejection(result.Error),
            $"Twilio account not provisioned for a test-mode success send: {result.Error}");

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderMessageId), "expected a Twilio Sid");
    }

    private static AlertMessage Msg(string to) =>
        new(Target: to, Subject: null, Body: "NekoHOA: your autopay attempt failed.");

    // Narrow match on the two environmental rejections a non-magic To can trip with test credentials.
    // Intentionally does NOT match auth/credential errors (e.g. 20003 Authenticate) — those must fail.
    private static bool IsAccountProvisioningRejection(string? error) =>
        error is not null
        && (error.Contains("is not a Twilio phone number", StringComparison.OrdinalIgnoreCase)        // 21606
            || error.Contains("Permission to send an SMS has not been enabled", StringComparison.OrdinalIgnoreCase)); // 21408
}

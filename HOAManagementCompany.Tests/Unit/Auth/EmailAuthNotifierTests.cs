using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Auth;

public class EmailAuthNotifierTests
{
    private sealed class FakeEmailProvider(AlertSendResult result) : IAlertProvider
    {
        public List<AlertMessage> Sent { get; } = [];
        public string Channel => "email";
        public bool IsConfigured => true;

        public Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task VerificationCode_IsDeliveredToTargetWithCodeInBody()
    {
        var provider = new FakeEmailProvider(AlertSendResult.Ok());
        var notifier = new EmailAuthNotifier(provider, NullLogger<EmailAuthNotifier>.Instance);

        await notifier.SendVerificationCodeAsync("owner@example.com", "123456");

        var msg = Assert.Single(provider.Sent);
        Assert.Equal("owner@example.com", msg.Target);
        Assert.Contains("123456", msg.Body);
        Assert.NotNull(msg.Subject);
    }

    [Fact]
    public async Task ClaimCode_IsDeliveredToTargetWithCodeInBody()
    {
        var provider = new FakeEmailProvider(AlertSendResult.Ok());
        var notifier = new EmailAuthNotifier(provider, NullLogger<EmailAuthNotifier>.Instance);

        await notifier.SendClaimCodeAsync("owner@example.com", "ABCD-1234");

        var msg = Assert.Single(provider.Sent);
        Assert.Contains("ABCD-1234", msg.Body);
    }

    [Fact]
    public async Task DeliveryFailure_IsSwallowed_NoExceptionLeaksToCaller()
    {
        var provider = new FakeEmailProvider(AlertSendResult.Fail("smtp down"));
        var notifier = new EmailAuthNotifier(provider, NullLogger<EmailAuthNotifier>.Instance);

        // Must not throw — uniform endpoint responses depend on it (FR-A1).
        await notifier.SendVerificationCodeAsync("owner@example.com", "123456");
    }
}

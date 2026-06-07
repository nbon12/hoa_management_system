using HOAManagementCompany.Infrastructure.Payments.Alerts;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// In-memory <see cref="IAlertProvider"/> for tests — records every accepted message and can be
/// forced to reject (to prove a rejection is terminal and never retried) or to report itself
/// unconfigured. No network calls.
/// </summary>
public sealed class FakeAlertProvider(string channel) : IAlertProvider
{
    public string Channel { get; } = channel;
    public bool IsConfigured { get; set; } = true;
    public bool RejectSends { get; set; }
    public List<AlertMessage> Sent { get; } = [];

    public Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (RejectSends) return Task.FromResult(AlertSendResult.Fail("forced rejection"));
        Sent.Add(message);
        return Task.FromResult(AlertSendResult.Ok($"fake_{Sent.Count}"));
    }
}

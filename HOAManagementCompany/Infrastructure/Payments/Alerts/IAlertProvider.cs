namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// A rendered alert ready to deliver. <see cref="Target"/> is the only PII permitted to leave the
/// process (the phone/email the owner opted in with); <see cref="Body"/> is masked and PII-free
/// (FR-016, FR-018). <see cref="Subject"/> is email-only.
/// </summary>
public sealed record AlertMessage(string Target, string? Subject, string Body);

/// <summary>Outcome of a single delivery attempt. A rejection is terminal — never retried (FR-019).</summary>
public sealed record AlertSendResult(bool Success, string? ProviderMessageId = null, string? Error = null)
{
    public static AlertSendResult Ok(string? providerMessageId = null) => new(true, providerMessageId);
    public static AlertSendResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// A delivery channel for failure alerts (SMS / email). Implementations are thin network adapters;
/// the channel-selection and outbox logic lives in <c>AlertService</c>/<c>OutboxDispatcher</c>.
/// </summary>
public interface IAlertProvider
{
    /// <summary><c>sms</c> or <c>email</c> — matches the channel half of <c>OutboxMessage.Kind</c>.</summary>
    string Channel { get; }

    /// <summary>True only when credentials are present; an unconfigured provider never sends.</summary>
    bool IsConfigured { get; }

    Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default);
}

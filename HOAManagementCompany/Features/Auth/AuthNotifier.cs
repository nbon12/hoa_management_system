using System.Diagnostics.CodeAnalysis;

namespace HOAManagementCompany.Features.Auth;

// 016-A FR-A1a/A3: out-of-band delivery of verification and claim codes.
public interface IAuthNotifier
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendClaimCodeAsync(string contact, string code, CancellationToken ct = default);
}

// Email delivery via the SendGrid alert provider. Delivery failures are logged (code withheld)
// and swallowed: verification/claim endpoints must return uniform responses regardless of
// delivery outcome (FR-A1), so an SMTP-level failure can never become an enumeration oracle.
public sealed class EmailAuthNotifier(
    Infrastructure.Payments.Alerts.IAlertProvider emailProvider,
    ILogger<EmailAuthNotifier> logger) : IAuthNotifier
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default) =>
        SendAsync(email, "Your NekoHOA verification code",
            $"Your NekoHOA email verification code is: {code}\n\n" +
            "It expires in 30 minutes. If you did not request this, you can ignore this email.", ct);

    public Task SendClaimCodeAsync(string contact, string code, CancellationToken ct = default) =>
        SendAsync(contact, "Your NekoHOA property claim code",
            $"Your NekoHOA property claim code is: {code}\n\n" +
            "Use it during registration to claim your property. It is single-use and expires in 90 days. " +
            "If you did not request this, contact your HOA office.", ct);

    private async Task SendAsync(string target, string subject, string body, CancellationToken ct)
    {
        var result = await emailProvider.SendAsync(
            new Infrastructure.Payments.Alerts.AlertMessage(target, subject, body), ct);
        if (!result.Success)
            logger.LogError("Auth code email delivery failed: {Error} (code withheld from logs).", result.Error);
    }
}

// Fallback delivery when no email provider is configured: structured audit log only (never logs
// the raw code). Keeps the security core testable without a network adapter.
[ExcludeFromCodeCoverage]
public sealed class LoggingAuthNotifier(ILogger<LoggingAuthNotifier> logger) : IAuthNotifier
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
    {
        logger.LogInformation("Verification code issued (delivery adapter not configured; code withheld from logs).");
        return Task.CompletedTask;
    }

    public Task SendClaimCodeAsync(string contact, string code, CancellationToken ct = default)
    {
        logger.LogInformation("Property claim code issued (delivery adapter not configured; code withheld from logs).");
        return Task.CompletedTask;
    }
}

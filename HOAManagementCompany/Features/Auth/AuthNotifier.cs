using System.Diagnostics.CodeAnalysis;

namespace HOAManagementCompany.Features.Auth;

// 016-A FR-A1a/A3: out-of-band delivery of verification and claim codes.
public interface IAuthNotifier
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendClaimCodeAsync(string contact, string code, CancellationToken ct = default);
}

// Default delivery: structured audit log only (never logs the raw code). Deployments swap in an
// email/SMS-backed implementation; this keeps the security core testable without a network adapter.
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

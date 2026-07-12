using System.Collections.Concurrent;
using HOAManagementCompany.Features.Auth;

namespace HOAManagementCompany.Features.DevTools;

// <!-- REPOWISE:START domain=devtools -->
// 020-D FR-D11: e2e test support for the verified-registration flow. Verification and claim
// codes are stored hashed and exist raw only inside the IAuthNotifier call, so the deployed
// registration e2e can only obtain them via this in-memory capture. The vault and its notifier
// decorator are registered ONLY when DevTools:E2ECleanupEnabled is set (Dev/PR/Test); they do
// not exist in Production/Staging containers, and the read endpoint is additionally gated like
// /e2e/cleanup (flag + shared secret + prod hard block).
// <!-- REPOWISE:END -->
public sealed class AuthCodeVault
{
    private readonly ConcurrentDictionary<string, string> _verification = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _claim = new(StringComparer.OrdinalIgnoreCase);

    public void StoreVerification(string contact, string code) => _verification[contact] = code;
    public void StoreClaim(string contact, string code) => _claim[contact] = code;

    public string? GetVerification(string contact) => _verification.TryGetValue(contact, out var c) ? c : null;
    public string? GetClaim(string contact) => _claim.TryGetValue(contact, out var c) ? c : null;
}

/// <summary>Decorates the real notifier: delivery proceeds unchanged, codes are also vaulted.</summary>
public sealed class VaultingAuthNotifier(IAuthNotifier inner, AuthCodeVault vault) : IAuthNotifier
{
    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
    {
        vault.StoreVerification(email, code);
        await inner.SendVerificationCodeAsync(email, code, ct);
    }

    public async Task SendClaimCodeAsync(string contact, string code, CancellationToken ct = default)
    {
        vault.StoreClaim(contact, code);
        await inner.SendClaimCodeAsync(contact, code, ct);
    }
}

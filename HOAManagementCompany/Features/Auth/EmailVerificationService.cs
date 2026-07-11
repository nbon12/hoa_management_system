using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Auth;

// 016-A FR-A3/A5: email-verification gate. Behaves uniformly regardless of account existence so it
// is not an enumeration oracle.
public class EmailVerificationService(
    ApplicationDbContext db,
    IAuthNotifier notifier,
    ILogger<EmailVerificationService> logger)
{
    public const string PurposeRegistration = "registration";
    public const string PurposeEmailChange = "email_change";

    private const int CodeExpiryMinutes = 30;
    private const int ProofExpiryMinutes = 15;
    private const int MaxAttempts = 5;

    public async Task RequestAsync(string email, string purpose, string? userId, CancellationToken ct)
    {
        var normalized = Normalize(email);
        var code = AuthCrypto.NewNumericCode();
        db.EmailVerifications.Add(new EmailVerification
        {
            Email = normalized,
            Purpose = purpose,
            UserId = userId,
            CodeHash = AuthCrypto.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(CodeExpiryMinutes)
        });
        await db.SaveChangesAsync(ct);
        await notifier.SendVerificationCodeAsync(normalized, code, ct);
        logger.LogInformation("Email verification requested for purpose {Purpose}", purpose);
    }

    // Returns an opaque proof token on success, or null (generic failure — no enumeration signal).
    public async Task<string?> ConfirmAsync(string email, string code, string purpose, CancellationToken ct)
    {
        var normalized = Normalize(email);
        var row = await db.EmailVerifications
            .Where(v => v.Email == normalized && v.Purpose == purpose && v.ConfirmedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (row is null || !row.CodeIsActive || row.AttemptCount >= MaxAttempts)
            return null;

        if (!AuthCrypto.HashesEqual(row.CodeHash, AuthCrypto.Hash(code)))
        {
            row.AttemptCount++;
            await db.SaveChangesAsync(ct);
            return null;
        }

        var proof = AuthCrypto.NewProofToken();
        row.ConfirmedAt = DateTimeOffset.UtcNow;
        row.ProofHash = AuthCrypto.Hash(proof);
        row.ProofExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ProofExpiryMinutes);
        await db.SaveChangesAsync(ct);
        return proof;
    }

    // Returns the (tracked) verified record for a proof token, or null. The caller marks it consumed
    // after the dependent operation (registration / email change) succeeds.
    public async Task<EmailVerification?> ResolveProofAsync(string proofToken, string purpose, CancellationToken ct)
    {
        var proofHash = AuthCrypto.Hash(proofToken);
        var row = await db.EmailVerifications
            .FirstOrDefaultAsync(v => v.ProofHash == proofHash && v.Purpose == purpose, ct);
        return row is not null && row.ProofIsActive ? row : null;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

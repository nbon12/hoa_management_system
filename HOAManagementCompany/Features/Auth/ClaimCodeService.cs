using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Auth;

// 016-A FR-A1/A1a: issue, deliver, and redeem single-use, 90-day property claim codes.
public class ClaimCodeService(
    ApplicationDbContext db,
    IAuthNotifier notifier,
    ILogger<ClaimCodeService> logger)
{
    private const int ValidityDays = 90;
    private const int MaxAttempts = 5;

    // Issue a fresh code for a property, superseding any prior unredeemed code, and deliver it.
    // Returns the raw code (for the admin/seeder that triggered issuance).
    public async Task<string> IssueAsync(Guid propertyId, string deliveredToContact, CancellationToken ct)
    {
        await db.PropertyClaimCodes
            .Where(c => c.PropertyId == propertyId && c.RedeemedAt == null)
            .ExecuteDeleteAsync(ct);

        var code = AuthCrypto.NewClaimCode();
        db.PropertyClaimCodes.Add(new PropertyClaimCode
        {
            PropertyId = propertyId,
            CodeHash = AuthCrypto.Hash(code),
            DeliveredToContact = Mask(deliveredToContact),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(ValidityDays)
        });
        await db.SaveChangesAsync(ct);
        await notifier.SendClaimCodeAsync(deliveredToContact, code, ct);
        logger.LogInformation("Claim code issued for property {PropertyId}", propertyId);
        return code;
    }

    // Returns the active (tracked) claim code for a raw value, or null. Increments the attempt
    // counter on a matched-but-invalid code; the caller marks it redeemed after success.
    public async Task<PropertyClaimCode?> FindActiveAsync(string rawCode, CancellationToken ct)
    {
        var hash = AuthCrypto.Hash(rawCode);
        var row = await db.PropertyClaimCodes.FirstOrDefaultAsync(c => c.CodeHash == hash, ct);
        if (row is null) return null;
        if (!row.IsActive || row.AttemptCount >= MaxAttempts)
        {
            row.AttemptCount++;
            await db.SaveChangesAsync(ct);
            return null;
        }
        return row;
    }

    private static string Mask(string contact) =>
        contact.Length <= 4 ? "***" : $"{contact[..2]}***{contact[^2..]}";
}

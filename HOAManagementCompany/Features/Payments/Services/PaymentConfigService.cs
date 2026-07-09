using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Payments.Services;

/// <summary>
/// Resolves the per-HOA <see cref="HoaPaymentConfig"/> for a property, falling back to the
/// deployment defaults in <see cref="PaymentsOptions"/> when a community has no stored row
/// (FR-004b). The fallback is transient (not persisted) so it stays safe by default.
/// </summary>
public sealed class PaymentConfigService(ApplicationDbContext db, IOptions<PaymentsOptions> options)
{
    /// <summary>Returns the stored config for the property's community, or a transient default.</summary>
    public async Task<HoaPaymentConfig> GetForPropertyAsync(Guid propertyId, CancellationToken ct = default)
    {
        var communityId = await db.Properties
            .Where(p => p.Id == propertyId)
            .Select(p => p.CommunityId)
            .FirstOrDefaultAsync(ct);

        return await GetForCommunityAsync(communityId ?? string.Empty, ct);
    }

    /// <summary>Returns the stored config for a community, or a transient default from options.</summary>
    public async Task<HoaPaymentConfig> GetForCommunityAsync(string communityId, CancellationToken ct = default)
    {
        var stored = await db.HoaPaymentConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, ct);

        return stored ?? BuildDefault(communityId, options.Value);
    }

    /// <summary>Materialises a default config from deployment options (FR-004b safe posture).</summary>
    public static HoaPaymentConfig BuildDefault(string communityId, PaymentsOptions opts)
    {
        var fee = opts.DefaultFee;
        return new HoaPaymentConfig
        {
            CommunityId = communityId,
            CardFeeType = Enum.TryParse<FeeType>(fee.CardFeeType, true, out var ft) ? ft : FeeType.Percentage,
            CardFeeValue = fee.CardFeeValue,
            CardScope = Enum.TryParse<CardScope>(fee.CardScope, true, out var cs) ? cs : CardScope.CreditOnly,
            SurchargingEnabled = fee.SurchargingEnabled,
            AchFeeValue = fee.AchFeeValue,
            NsfFeeEnabled = opts.Nsf.Enabled,
            NsfFeeAmount = opts.Nsf.Amount,
            VariableNoticeLeadDays = opts.VariableNoticeLeadDays,
        };
    }
}

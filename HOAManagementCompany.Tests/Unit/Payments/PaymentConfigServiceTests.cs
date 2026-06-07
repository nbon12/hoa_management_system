using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Features.Payments.Services;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Payments;

/// <summary>
/// <see cref="PaymentConfigService.BuildDefault"/> materialises a per-HOA fee policy from deployment
/// options when no stored <c>HoaPaymentConfig</c> exists (FR-004b safe posture).
/// </summary>
public class PaymentConfigServiceTests
{
    [Fact]
    public void BuildDefault_MapsDeploymentOptions()
    {
        var opts = new PaymentsOptions
        {
            VariableNoticeLeadDays = 14,
            DefaultFee = new PaymentsOptions.FeeOptions
            {
                CardFeeType = "Flat",
                CardFeeValue = 1.95m,
                CardScope = "AllCards",
                AchFeeValue = 0.50m,
                SurchargingEnabled = true,
            },
            Nsf = new PaymentsOptions.NsfOptions { Enabled = true, Amount = 25m },
        };

        var cfg = PaymentConfigService.BuildDefault("SAKURA", opts);

        Assert.Equal("SAKURA", cfg.CommunityId);
        Assert.Equal(FeeType.Flat, cfg.CardFeeType);
        Assert.Equal(1.95m, cfg.CardFeeValue);
        Assert.Equal(CardScope.AllCards, cfg.CardScope);
        Assert.Equal(0.50m, cfg.AchFeeValue);
        Assert.True(cfg.SurchargingEnabled);
        Assert.True(cfg.NsfFeeEnabled);
        Assert.Equal(25m, cfg.NsfFeeAmount);
        Assert.Equal(14, cfg.VariableNoticeLeadDays);
    }

    [Fact]
    public void BuildDefault_UnparseableEnums_FallBackToSafePosture()
    {
        var opts = new PaymentsOptions
        {
            DefaultFee = new PaymentsOptions.FeeOptions { CardFeeType = "nonsense", CardScope = "bogus" },
        };

        var cfg = PaymentConfigService.BuildDefault("X", opts);

        Assert.Equal(FeeType.Percentage, cfg.CardFeeType);   // safe default when the option is garbage
        Assert.Equal(CardScope.CreditOnly, cfg.CardScope);   // percentage requires credit-only
    }
}

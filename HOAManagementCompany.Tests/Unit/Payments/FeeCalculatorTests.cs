using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Services;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Payments;

public class FeeCalculatorTests
{
    private readonly FeeCalculator _calc = new();

    private static HoaPaymentConfig Config(
        FeeType cardFeeType = FeeType.Percentage, decimal cardFeeValue = 0.03m,
        CardScope scope = CardScope.CreditOnly, bool surcharging = true, decimal achFee = 0m) =>
        new()
        {
            CardFeeType = cardFeeType,
            CardFeeValue = cardFeeValue,
            CardScope = scope,
            SurchargingEnabled = surcharging,
            AchFeeValue = achFee,
        };

    [Fact]
    public void Ach_ChargesFlatAchFee()
    {
        var result = _calc.Calculate(250m, PaymentMethod.Ach, null, Config(achFee: 1.50m));
        Assert.Equal(250m, result.Gross);
        Assert.Equal(1.50m, result.Fee);
        Assert.Equal(251.50m, result.Total);
    }

    [Fact]
    public void Card_FlatFee_AppliesToAllFundingTypes()
    {
        var config = Config(cardFeeType: FeeType.Flat, cardFeeValue: 2.95m, scope: CardScope.AllCards);
        var debit = _calc.Calculate(100m, PaymentMethod.Card, CardFunding.Debit, config);
        Assert.Equal(2.95m, debit.Fee);
        Assert.Equal(102.95m, debit.Total);
    }

    [Fact]
    public void Card_PercentageSurcharge_AppliesToCreditOnly()
    {
        var result = _calc.Calculate(200m, PaymentMethod.Card, CardFunding.Credit, Config());
        Assert.Equal(6.00m, result.Fee);          // 200 * 0.03
        Assert.Equal(206.00m, result.Total);
    }

    [Theory]
    [InlineData(CardFunding.Debit)]
    [InlineData(CardFunding.Prepaid)]
    [InlineData(CardFunding.Unknown)]
    public void Card_PercentageSurcharge_NotAppliedToNonCredit(CardFunding funding)
    {
        var result = _calc.Calculate(200m, PaymentMethod.Card, funding, Config());
        Assert.Equal(0m, result.Fee);
        Assert.Equal(200m, result.Total);
    }

    [Fact]
    public void Card_PercentageSurcharge_NullFunding_NoFee()
    {
        var result = _calc.Calculate(200m, PaymentMethod.Card, null, Config());
        Assert.Equal(0m, result.Fee);
    }

    [Fact]
    public void Card_PercentageSurcharge_DisabledByJurisdiction_NoFee()
    {
        var result = _calc.Calculate(200m, PaymentMethod.Card, CardFunding.Credit, Config(surcharging: false));
        Assert.Equal(0m, result.Fee);
    }

    [Fact]
    public void Percentage_RoundsAwayFromZero()
    {
        // 99.99 * 0.03 = 2.9997 → 3.00
        var result = _calc.Calculate(99.99m, PaymentMethod.Card, CardFunding.Credit, Config());
        Assert.Equal(3.00m, result.Fee);
    }

    [Fact]
    public void ValidateConfig_PercentageWithAllCards_Throws()
    {
        var config = Config(cardFeeType: FeeType.Percentage, scope: CardScope.AllCards);
        Assert.Throws<ArgumentException>(() => FeeCalculator.ValidateConfig(config));
    }

    [Fact]
    public void Calculate_PercentageWithAllCards_Throws()
    {
        var config = Config(cardFeeType: FeeType.Percentage, scope: CardScope.AllCards);
        Assert.Throws<ArgumentException>(() =>
            _calc.Calculate(100m, PaymentMethod.Card, CardFunding.Credit, config));
    }
}

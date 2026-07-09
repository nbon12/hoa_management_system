using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.UnitTests.Configuration;

/// <summary>
/// Locks in the payments fee-policy rules that were previously comment-only (008 FR-006/FR-007).
/// </summary>
public class PaymentsOptionsValidatorTests
{
    private static readonly PaymentsOptionsValidator Validator = new();

    private static PaymentsOptions Valid() => new()
    {
        VariableNoticeLeadDays = 10,
        ReconcilePendingAchAfterHours = 96,
        DefaultFee = new PaymentsOptions.FeeOptions
        {
            CardFeeType = "Percentage",
            CardScope = "CreditOnly",
            CardFeeValue = 0.03m,
            AchFeeValue = 0m,
            SurchargingEnabled = true,
        },
        Nsf = new PaymentsOptions.NsfOptions { Enabled = false, Amount = 25m },
    };

    [Fact]
    public void DefaultValidConfig_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("Flat")]
    [InlineData("Percentage")]
    [InlineData("flat")]        // case-insensitive
    [InlineData("PERCENTAGE")]  // case-insensitive
    public void CardFeeType_KnownValues_Pass(string feeType)
    {
        var o = Valid();
        o.DefaultFee.CardFeeType = feeType;
        o.DefaultFee.CardScope = "CreditOnly"; // satisfies the percentage cross-field rule
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tiered")]
    [InlineData("flatt")]
    public void CardFeeType_UnknownValues_Fail(string feeType)
    {
        var o = Valid();
        o.DefaultFee.CardFeeType = feeType;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("AllCards")]
    [InlineData("CreditOnly")]
    [InlineData("creditonly")]
    public void CardScope_KnownValues_Pass(string scope)
    {
        var o = Valid();
        o.DefaultFee.CardFeeType = "Flat"; // avoid the percentage cross-field rule here
        o.DefaultFee.CardScope = scope;
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void PercentageFee_WithAllCardsScope_Fails()
    {
        var o = Valid();
        o.DefaultFee.CardFeeType = "Percentage";
        o.DefaultFee.CardScope = "AllCards";
        var result = Validator.Validate(o);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("CreditOnly"));
    }

    [Fact]
    public void FlatFee_WithAllCardsScope_Passes()
    {
        var o = Valid();
        o.DefaultFee.CardFeeType = "Flat";
        o.DefaultFee.CardScope = "AllCards";
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(0)]   // allowed
    public void VariableNoticeLeadDays_Zero_Passes(int days)
    {
        var o = Valid();
        o.VariableNoticeLeadDays = days;
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void VariableNoticeLeadDays_Negative_Fails(int days)
    {
        var o = Valid();
        o.VariableNoticeLeadDays = days;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ReconcilePendingAchAfterHours_NonPositive_Fails(int hours)
    {
        var o = Valid();
        o.ReconcilePendingAchAfterHours = hours;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void NegativeCardFeeValue_Fails()
    {
        var o = Valid();
        o.DefaultFee.CardFeeValue = -0.01m;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Fact]
    public void NegativeNsfAmount_Fails()
    {
        var o = Valid();
        o.Nsf.Amount = -1m;
        Assert.False(Validator.Validate(o).IsValid);
    }
}

using HOAManagementCompany.Domain.Payments;
using Xunit;

namespace HOAManagementCompany.UnitTests.Payments;

/// <summary>015 US5 (FR-015): the single monetary policy — conversion + rounding.</summary>
public class MoneyPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1.00, 100)]
    [InlineData(250.00, 25000)]
    [InlineData(10.005, 1001)]     // midpoint rounds away from zero
    [InlineData(10.004, 1000)]
    [InlineData(0.005, 1)]
    public void ToCents_RoundsAwayFromZero(decimal amount, long expected) =>
        Assert.Equal(expected, MoneyPolicy.ToCents(amount));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 1.00)]
    [InlineData(12345, 123.45)]
    public void FromCents_ConvertsToMajorUnits(long cents, decimal expected) =>
        Assert.Equal(expected, MoneyPolicy.FromCents(cents));

    [Fact]
    public void RoundTrip_IsStable_ForCentPrecisionAmounts() =>
        Assert.Equal(19.99m, MoneyPolicy.FromCents(MoneyPolicy.ToCents(19.99m)));

    [Fact]
    public void Currency_IsUsd() => Assert.Equal("usd", MoneyPolicy.Currency);
}

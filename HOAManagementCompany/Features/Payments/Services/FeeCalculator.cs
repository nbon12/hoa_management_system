using HOAManagementCompany.Domain.Payments;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Payments.Services;

/// <summary>Result of a fee computation — the principal, the fee, and the amount actually charged.</summary>
public readonly record struct FeeResult(decimal Gross, decimal Fee, decimal Total);

/// <summary>
/// Computes the resident-facing convenience fee / surcharge per the per-HOA
/// <see cref="HoaPaymentConfig"/> and the card funding type reported by Stripe (FR-004b/d).
/// Stateless and deterministic so it is trivially unit-testable.
/// </summary>
public sealed class FeeCalculator
{
    /// <summary>
    /// Rejects an illegal fee policy: a percentage fee is a credit-card *surcharge* and may only
    /// target credit cards — debit/prepaid are never percentage-surcharged (FR-004b).
    /// </summary>
    public static void ValidateConfig(HoaPaymentConfig config)
    {
        if (config.CardFeeType == FeeType.Percentage && config.CardScope == CardScope.AllCards)
            throw new ArgumentException(
                "A percentage card fee must be credit-only (CardScope.CreditOnly); " +
                "debit and prepaid cards cannot be percentage-surcharged.",
                nameof(config));
    }

    /// <summary>
    /// Splits <paramref name="gross"/> into gross + fee + total.
    /// ACH is charged the (default-zero) flat ACH fee; a flat card fee is a convenience fee on all
    /// cards; a percentage card fee is a surcharge applied only to credit cards when surcharging is
    /// enabled. Rounded away-from-zero to cents.
    /// </summary>
    public FeeResult Calculate(decimal gross, PaymentMethod method, CardFunding? funding, HoaPaymentConfig config)
    {
        ValidateConfig(config);

        decimal fee;
        if (method == PaymentMethod.Ach)
        {
            fee = config.AchFeeValue;
        }
        else if (config.CardFeeType == FeeType.Flat)
        {
            // Convenience fee — applies to all card funding types.
            fee = config.CardFeeValue;
        }
        else
        {
            // Percentage surcharge — credit-only, and only when the jurisdiction allows it.
            var f = funding ?? CardFunding.Unknown;
            fee = config.SurchargingEnabled && f == CardFunding.Credit
                ? gross * config.CardFeeValue
                : 0m;
        }

        fee = Math.Round(fee, 2, MoneyPolicy.Rounding);
        return new FeeResult(gross, fee, gross + fee);
    }
}

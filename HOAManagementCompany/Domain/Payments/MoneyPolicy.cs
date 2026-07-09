namespace HOAManagementCompany.Domain.Payments;

/// <summary>
/// The single definition of the platform's monetary policies (015 US5, FR-015): major↔minor unit
/// conversion, the rounding mode, and the currency designation. Internally amounts are
/// <see cref="decimal"/> major units everywhere; minor units (cents) exist only at the payment
/// provider boundary.
/// </summary>
public static class MoneyPolicy
{
    /// <summary>The platform's (only) settlement currency.</summary>
    public const string Currency = "usd";

    /// <summary>The rounding policy for monetary calculations (fees, minor-unit conversion).</summary>
    public const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    /// <summary>Major units → provider minor units (cents), rounded per <see cref="Rounding"/>.</summary>
    public static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, Rounding);

    /// <summary>Provider minor units (cents) → major units.</summary>
    public static decimal FromCents(long cents) => cents / 100m;
}

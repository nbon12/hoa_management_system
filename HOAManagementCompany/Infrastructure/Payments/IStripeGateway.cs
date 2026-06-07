using HOAManagementCompany.Domain.Enums;
using Stripe;

namespace HOAManagementCompany.Infrastructure.Payments;

/// <summary>Inputs for creating a one-time PaymentIntent.</summary>
public sealed record CreatePaymentIntentRequest(
    long AmountCents,
    string Currency,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IdempotencyKey = null);

/// <summary>Gateway-neutral view of a PaymentIntent, including masked method details when settled.</summary>
public sealed record StripePaymentIntentResult(
    string Id,
    string ClientSecret,
    string Status,
    long AmountReceived,
    string Currency,
    string? PaymentMethodType,
    string? LatestChargeId,
    CardFunding? CardFunding,
    string? CardBrand,
    string? Last4,
    string? FailureCode = null,
    string? FailureMessage = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>Settlement detail for reconciliation (FR-037).</summary>
public sealed record StripeChargeResult(
    string ChargeId,
    string? BalanceTransactionId,
    decimal? ProcessorFeeAmount,
    string? PayoutId,
    decimal AmountRefunded);

/// <summary>
/// Abstraction over the Stripe SDK so payment flows are testable behind an in-memory fake
/// (no network in tests). The MVP surface covers one-time PaymentIntents, charge/settlement
/// lookups, and signature-verified webhook event construction; vaulting/off-session charges are
/// added with the recurring story.
/// </summary>
public interface IStripeGateway
{
    /// <summary>Creates a PaymentIntent (dynamic payment methods — no <c>payment_method_types</c>).</summary>
    Task<StripePaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken ct = default);

    /// <summary>Fetches a PaymentIntent with payment-method details expanded.</summary>
    Task<StripePaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);

    /// <summary>Fetches settlement detail for a charge (balance transaction, processor fee, payout).</summary>
    Task<StripeChargeResult?> GetChargeAsync(string chargeId, CancellationToken ct = default);

    /// <summary>Verifies the webhook signature and returns the parsed event, or throws on failure.</summary>
    Event ConstructEvent(string json, string signatureHeader);
}

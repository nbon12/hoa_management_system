using HOAManagementCompany.Domain.Enums;
using Stripe;

namespace HOAManagementCompany.Infrastructure.Payments;

/// <summary>Inputs for creating a one-time PaymentIntent.</summary>
public sealed record CreatePaymentIntentRequest(
    long AmountCents,
    string Currency,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IdempotencyKey = null);

/// <summary>Inputs for charging a vaulted payment method off-session (recurring draft, FR-010).</summary>
public sealed record CreateOffSessionChargeRequest(
    string CustomerId,
    string PaymentMethodId,
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

/// <summary>Gateway-neutral view of a SetupIntent used to vault a method on file (FR-009).</summary>
public sealed record StripeSetupIntentResult(
    string Id,
    string ClientSecret,
    string? CustomerId,
    string Status);

/// <summary>
/// The vaulted payment method resolved from a completed SetupIntent — only references and masked
/// display detail are returned; no raw PAN/bank number ever reaches the backend (SC-001).
/// </summary>
public sealed record StripeVaultedMethod(
    string PaymentMethodId,
    string? MandateId,
    string? PaymentMethodType,
    CardFunding? CardFunding,
    string? Brand,
    string? Last4);

/// <summary>
/// Abstraction over the Stripe SDK so payment flows are testable behind an in-memory fake
/// (no network in tests). Covers one-time PaymentIntents, charge/settlement lookups,
/// signature-verified webhook event construction, and the recurring vaulting/off-session surface.
/// </summary>
public interface IStripeGateway
{
    /// <summary>Creates a PaymentIntent (dynamic payment methods — no explicit method types).</summary>
    Task<StripePaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken ct = default);

    /// <summary>Fetches a PaymentIntent with payment-method details expanded.</summary>
    Task<StripePaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);

    /// <summary>Fetches settlement detail for a charge (balance transaction, processor fee, payout).</summary>
    Task<StripeChargeResult?> GetChargeAsync(string chargeId, CancellationToken ct = default);

    /// <summary>Verifies the webhook signature and returns the parsed event, or throws on failure.</summary>
    Event ConstructEvent(string json, string signatureHeader);

    /// <summary>
    /// Returns the Stripe customer id for a resident, creating one if <paramref name="existingCustomerId"/>
    /// is null/blank and reusing it otherwise (FR-009). The customer anchors vaulted methods off-session.
    /// </summary>
    Task<string> EnsureCustomerAsync(string? existingCustomerId, string email, string? name, CancellationToken ct = default);

    /// <summary>Creates a SetupIntent for the customer so the browser can vault a method on file (FR-009).</summary>
    Task<StripeSetupIntentResult> CreateSetupIntentAsync(string customerId, CancellationToken ct = default);

    /// <summary>Resolves the vaulted payment method (and mandate) from a completed SetupIntent.</summary>
    Task<StripeVaultedMethod> GetSetupIntentResultAsync(string setupIntentId, CancellationToken ct = default);

    /// <summary>Charges a vaulted method off-session for a recurring draft (FR-010).</summary>
    Task<StripePaymentIntentResult> ChargeOffSessionAsync(CreateOffSessionChargeRequest request, CancellationToken ct = default);
}

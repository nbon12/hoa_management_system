using System.Collections.Concurrent;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using Stripe;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// In-memory <see cref="IStripeGateway"/> for integration tests — no network calls. Tests drive
/// PaymentIntent outcomes by configuring <see cref="SetOutcome"/> after creating an intent, and
/// build webhook events from raw JSON via <see cref="EventUtility.ParseEvent(string)"/> (offline,
/// signature not verified).
/// </summary>
public sealed class FakeStripeGateway : IStripeGateway
{
    private readonly ConcurrentDictionary<string, StripePaymentIntentResult> _outcomes = new();
    private readonly ConcurrentDictionary<string, StripeChargeResult> _charges = new();
    private readonly ConcurrentDictionary<string, StripeVaultedMethod> _vaultedMethods = new();
    private readonly ConcurrentDictionary<string, StripePaymentIntentResult> _offSessionOutcomes = new();

    /// <summary>PaymentIntents created via this gateway, in creation order (for assertions).</summary>
    public List<CreatePaymentIntentRequest> CreatedIntents { get; } = new();

    /// <summary>Off-session charges created via this gateway, in order (for recurring-draft assertions).</summary>
    public List<CreateOffSessionChargeRequest> OffSessionCharges { get; } = new();

    /// <summary>SetupIntents created via this gateway (for assertions).</summary>
    public List<string> CreatedSetupIntents { get; } = new();

    /// <summary>Overrides the result returned by <see cref="GetPaymentIntentAsync"/> for an intent.</summary>
    public void SetOutcome(string paymentIntentId, StripePaymentIntentResult result) =>
        _outcomes[paymentIntentId] = result;

    /// <summary>Registers a settlement result returned by <see cref="GetChargeAsync"/>.</summary>
    public void SetCharge(string chargeId, StripeChargeResult result) => _charges[chargeId] = result;

    /// <summary>Registers the vaulted method a given SetupIntent resolves to (else a default card is used).</summary>
    public void SetVaultedMethod(string setupIntentId, StripeVaultedMethod method) =>
        _vaultedMethods[setupIntentId] = method;

    /// <summary>Forces the outcome of an off-session charge against a vaulted payment method.</summary>
    public void SetOffSessionOutcome(string paymentMethodId, StripePaymentIntentResult result) =>
        _offSessionOutcomes[paymentMethodId] = result;

    public Task<StripePaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken ct = default)
    {
        CreatedIntents.Add(request);
        var id = $"pi_test_{Guid.NewGuid():N}";
        var result = new StripePaymentIntentResult(
            id, $"{id}_secret_test", "requires_payment_method",
            AmountReceived: 0, request.Currency, PaymentMethodType: null, LatestChargeId: null,
            CardFunding: null, CardBrand: null, Last4: null, Metadata: request.Metadata);
        // Default to a successful credit-card outcome so happy-path confirm works without setup.
        _outcomes[id] = result with
        {
            Status = "succeeded",
            AmountReceived = request.AmountCents,
            PaymentMethodType = "card",
            LatestChargeId = $"ch_test_{Guid.NewGuid():N}",
            CardFunding = CardFunding.Credit,
            CardBrand = "visa",
            Last4 = "4242",
        };
        return Task.FromResult(result);
    }

    public Task<StripePaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default) =>
        Task.FromResult(_outcomes.TryGetValue(paymentIntentId, out var r)
            ? r
            : new StripePaymentIntentResult(paymentIntentId, $"{paymentIntentId}_secret_test", "succeeded",
                0, "usd", "card", $"ch_test_{Guid.NewGuid():N}", CardFunding.Credit, "visa", "4242"));

    public Task<StripeChargeResult?> GetChargeAsync(string chargeId, CancellationToken ct = default) =>
        Task.FromResult(_charges.TryGetValue(chargeId, out var r)
            ? r
            : new StripeChargeResult(chargeId, $"txn_test_{Guid.NewGuid():N}", 0m, null, 0m));

    /// <summary>
    /// Offline event construction. A <c>signatureHeader</c> of <c>"invalid"</c> simulates a failed
    /// signature check (the real adapter throws <see cref="StripeException"/>); otherwise the raw
    /// JSON is parsed without signature verification.
    /// </summary>
    public PaymentProviderEvent ParseEvent(string json, string signatureHeader)
    {
        if (signatureHeader == "invalid")
            throw new ProviderSignatureVerificationException("Invalid signature");
        return StripeEventTranslator.Translate(EventUtility.ParseEvent(json));
    }

    public PaymentProviderEvent ParseStoredEvent(string json) =>
        StripeEventTranslator.Translate(EventUtility.ParseEvent(json));

    public Task<string> EnsureCustomerAsync(string? existingCustomerId, string email, string? name, CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(existingCustomerId) ? $"cus_test_{Guid.NewGuid():N}" : existingCustomerId);

    public Task<StripeSetupIntentResult> CreateSetupIntentAsync(string customerId, CancellationToken ct = default)
    {
        var id = $"seti_test_{Guid.NewGuid():N}";
        CreatedSetupIntents.Add(id);
        return Task.FromResult(new StripeSetupIntentResult(id, $"{id}_secret_test", customerId, "requires_payment_method"));
    }

    public Task<StripeVaultedMethod> GetSetupIntentResultAsync(string setupIntentId, CancellationToken ct = default) =>
        Task.FromResult(_vaultedMethods.TryGetValue(setupIntentId, out var m)
            ? m
            // Default: a vaulted credit card with a mandate, so the happy-path setup works without configuration.
            : new StripeVaultedMethod($"pm_test_{Guid.NewGuid():N}", $"mandate_test_{Guid.NewGuid():N}",
                "card", CardFunding.Credit, "visa", "4242"));

    public Task<StripePaymentIntentResult> ChargeOffSessionAsync(CreateOffSessionChargeRequest request, CancellationToken ct = default)
    {
        OffSessionCharges.Add(request);
        if (_offSessionOutcomes.TryGetValue(request.PaymentMethodId, out var forced))
            return Task.FromResult(forced);

        // Default: a successful off-session card charge for the requested amount.
        var id = $"pi_test_{Guid.NewGuid():N}";
        return Task.FromResult(new StripePaymentIntentResult(
            id, $"{id}_secret_test", "succeeded", request.AmountCents, request.Currency,
            "card", $"ch_test_{Guid.NewGuid():N}", CardFunding.Credit, "visa", "4242",
            Metadata: request.Metadata));
    }
}

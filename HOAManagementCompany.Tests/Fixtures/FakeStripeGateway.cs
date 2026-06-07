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

    /// <summary>PaymentIntents created via this gateway, in creation order (for assertions).</summary>
    public List<CreatePaymentIntentRequest> CreatedIntents { get; } = new();

    /// <summary>Overrides the result returned by <see cref="GetPaymentIntentAsync"/> for an intent.</summary>
    public void SetOutcome(string paymentIntentId, StripePaymentIntentResult result) =>
        _outcomes[paymentIntentId] = result;

    /// <summary>Registers a settlement result returned by <see cref="GetChargeAsync"/>.</summary>
    public void SetCharge(string chargeId, StripeChargeResult result) => _charges[chargeId] = result;

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
    public Event ConstructEvent(string json, string signatureHeader)
    {
        if (signatureHeader == "invalid") throw new StripeException("Invalid signature");
        return EventUtility.ParseEvent(json);
    }
}

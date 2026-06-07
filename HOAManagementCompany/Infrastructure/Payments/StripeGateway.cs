using System.Diagnostics.CodeAnalysis;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments;
using Microsoft.Extensions.Options;
using Stripe;

namespace HOAManagementCompany.Infrastructure.Payments;

/// <summary>
/// Thin adapter over the Stripe.net SDK. Excluded from coverage — every method is a direct
/// network call to Stripe and is exercised via the in-memory <c>FakeStripeGateway</c> in tests
/// (per the testing constitution: no real external calls in CI).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class StripeGateway : IStripeGateway
{
    private readonly Lazy<StripeClient> _client;
    private readonly StripeOptions _options;

    public StripeGateway(IOptions<StripeOptions> options)
    {
        _options = options.Value;
        // Build the SDK client lazily: StripeClient throws on an empty key, and the DI container
        // constructs this singleton at startup even in environments with no configured secret
        // (CI/tests, where the in-memory FakeStripeGateway is used instead). Defer the throw to
        // first real use so a missing key only fails when Stripe is actually called.
        _client = new Lazy<StripeClient>(() => new StripeClient(_options.SecretKey));
    }

    private StripeClient Client => _client.Value;

    public async Task<StripePaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken ct = default)
    {
        var service = new PaymentIntentService(Client);
        var options = new PaymentIntentCreateOptions
        {
            Amount = request.AmountCents,
            Currency = request.Currency,
            // Omit PaymentMethodTypes — dynamic payment methods configured from the Dashboard.
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = request.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value),
        };
        var requestOptions = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : new RequestOptions { IdempotencyKey = request.IdempotencyKey };
        var pi = await service.CreateAsync(options, requestOptions, ct);
        return Map(pi);
    }

    public async Task<StripePaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var service = new PaymentIntentService(Client);
        var pi = await service.GetAsync(paymentIntentId, new PaymentIntentGetOptions
        {
            Expand = ["latest_charge", "latest_charge.payment_method_details", "payment_method"],
        }, cancellationToken: ct);
        return Map(pi);
    }

    public async Task<StripeChargeResult?> GetChargeAsync(string chargeId, CancellationToken ct = default)
    {
        var service = new ChargeService(Client);
        var charge = await service.GetAsync(chargeId, new ChargeGetOptions { Expand = ["balance_transaction"] }, cancellationToken: ct);
        if (charge is null) return null;
        return new StripeChargeResult(
            charge.Id,
            charge.BalanceTransactionId,
            charge.BalanceTransaction is not null ? charge.BalanceTransaction.Fee / 100m : null,
            null,
            charge.AmountRefunded / 100m);
    }

    public Event ConstructEvent(string json, string signatureHeader) =>
        EventUtility.ConstructEvent(json, signatureHeader, _options.WebhookSigningSecret,
            tolerance: _options.WebhookToleranceSeconds);

    public async Task<string> EnsureCustomerAsync(string? existingCustomerId, string email, string? name, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(existingCustomerId)) return existingCustomerId;
        var service = new CustomerService(Client);
        var customer = await service.CreateAsync(new CustomerCreateOptions { Email = email, Name = name }, cancellationToken: ct);
        return customer.Id;
    }

    public async Task<StripeSetupIntentResult> CreateSetupIntentAsync(string customerId, CancellationToken ct = default)
    {
        var service = new SetupIntentService(Client);
        var si = await service.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = customerId,
            Usage = "off_session",
            AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions { Enabled = true },
        }, cancellationToken: ct);
        return new StripeSetupIntentResult(si.Id, si.ClientSecret, si.CustomerId, si.Status);
    }

    public async Task<StripeVaultedMethod> GetSetupIntentResultAsync(string setupIntentId, CancellationToken ct = default)
    {
        var service = new SetupIntentService(Client);
        var si = await service.GetAsync(setupIntentId, new SetupIntentGetOptions { Expand = ["payment_method", "mandate"] }, cancellationToken: ct);
        var pm = si.PaymentMethod;
        var card = pm?.Card;
        return new StripeVaultedMethod(
            si.PaymentMethodId,
            si.MandateId,
            pm?.Type,
            MapFunding(card?.Funding),
            card?.Brand,
            card?.Last4 ?? pm?.UsBankAccount?.Last4);
    }

    public async Task<StripePaymentIntentResult> ChargeOffSessionAsync(CreateOffSessionChargeRequest request, CancellationToken ct = default)
    {
        var service = new PaymentIntentService(Client);
        var options = new PaymentIntentCreateOptions
        {
            Amount = request.AmountCents,
            Currency = request.Currency,
            Customer = request.CustomerId,
            PaymentMethod = request.PaymentMethodId,
            Confirm = true,
            OffSession = true,
            Metadata = request.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value),
        };
        var requestOptions = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : new RequestOptions { IdempotencyKey = request.IdempotencyKey };
        try
        {
            var pi = await service.CreateAsync(options, requestOptions, ct);
            return Map(pi);
        }
        catch (StripeException ex) when (ex.StripeError?.PaymentIntent is not null)
        {
            // Off-session authentication/charge failure surfaces the PaymentIntent on the error.
            return Map(ex.StripeError.PaymentIntent);
        }
    }

    private static StripePaymentIntentResult Map(PaymentIntent pi)
    {
        var charge = pi.LatestCharge;
        var card = charge?.PaymentMethodDetails?.Card;
        return new StripePaymentIntentResult(
            pi.Id,
            pi.ClientSecret,
            pi.Status,
            pi.AmountReceived,
            pi.Currency,
            charge?.PaymentMethodDetails?.Type,
            pi.LatestChargeId,
            MapFunding(card?.Funding),
            card?.Brand,
            card?.Last4,
            charge?.FailureCode,
            charge?.FailureMessage,
            pi.Metadata);
    }

    private static CardFunding? MapFunding(string? funding) => funding switch
    {
        "credit" => CardFunding.Credit,
        "debit" => CardFunding.Debit,
        "prepaid" => CardFunding.Prepaid,
        null => null,
        _ => CardFunding.Unknown,
    };
}

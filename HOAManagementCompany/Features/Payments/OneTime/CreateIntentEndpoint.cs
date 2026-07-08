using HOAManagementCompany.Features.Common;
using System.Globalization;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Payments;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Features.Payments.OneTime;

/// <summary>
/// POST /payments/intent — computes the fee server-side, creates a Stripe PaymentIntent with the
/// gross/fee split stored in metadata (server-authoritative), and forwards the client idempotency
/// key to Stripe (FR-007a/b). No raw card data is accepted (SC-001).
/// </summary>
public class CreateIntentEndpoint(IStripeGateway gateway, FeeCalculator feeCalculator, PaymentConfigService config)
    : Endpoint<CreateIntentRequest, CreateIntentResponse>
{
    public override void Configure()
    {
        Post("/payments/intent");
        Description(x => x.WithName("CreatePaymentIntent").WithTags("Payments").RequireRateLimiting("payments"));
    }

    public override async Task HandleAsync(CreateIntentRequest req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var method = req.Method.Equals("card", StringComparison.OrdinalIgnoreCase) ? PaymentMethod.Card : PaymentMethod.Ach;

        var cfg = await config.GetForPropertyAsync(propertyId, ct);
        // Card funding is unknown until the element is filled; assume credit for the surcharge estimate.
        var funding = method == PaymentMethod.Card ? CardFunding.Credit : (CardFunding?)null;
        var fee = feeCalculator.Calculate(req.Amount, method, funding, cfg);

        var amountCents = (long)Math.Round(fee.Total * 100m, MidpointRounding.AwayFromZero);
        var idempotencyKey = HttpContext.Request.Headers[IdempotencyService.HeaderName].FirstOrDefault();
        var metadata = new Dictionary<string, string>
        {
            ["propertyId"] = propertyId.ToString(),
            ["grossAmount"] = fee.Gross.ToString(CultureInfo.InvariantCulture),
            ["feeAmount"] = fee.Fee.ToString(CultureInfo.InvariantCulture),
            ["method"] = method.ToString(),
        };

        var pi = await gateway.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(amountCents, "usd", metadata, idempotencyKey), ct);

        await SendOkAsync(new CreateIntentResponse(pi.Id, pi.ClientSecret, fee.Gross, fee.Fee, fee.Total), ct);
    }
}

public class CreateIntentValidator : Validator<CreateIntentRequest>
{
    public CreateIntentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).NotEmpty().Must(m => m is "ach" or "card")
            .WithMessage("Method must be 'ach' or 'card'.");
    }
}

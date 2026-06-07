using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments.Recurring;

public class RecurringUpsertEndpoint(PaymentService paymentService) : Endpoint<RecurringPaymentRequest, RecurringPaymentDto>
{
    public override void Configure()
    {
        Put("/payments/recurring");
        Description(x => x.WithName("UpsertRecurring").WithTags("Payments"));
    }

    public override async Task HandleAsync(RecurringPaymentRequest req, CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();
        var result = await paymentService.UpsertRecurringAsync(propertyId, req, ip, userAgent, ct);
        await SendOkAsync(result, ct);
    }
}

public class RecurringPaymentValidator : Validator<RecurringPaymentRequest>
{
    public RecurringPaymentValidator()
    {
        RuleFor(x => x.AmountType).NotEmpty()
            .Must(a => a is "fixed" or "assessment" or "balance")
            .WithMessage("amountType must be one of: fixed, assessment, balance.");
        RuleFor(x => x.DraftDay).InclusiveBetween(1, 28);
        RuleFor(x => x.SetupIntentId).NotEmpty()
            .WithMessage("setupIntentId is required — vault a payment method first.");
        RuleFor(x => x.MandateAccepted).Equal(true)
            .WithMessage("The recurring payment mandate must be accepted.");
        RuleFor(x => x.FixedAmount).NotNull().GreaterThan(0)
            .When(x => x.AmountType?.Equals("fixed", StringComparison.OrdinalIgnoreCase) == true)
            .WithMessage("fixedAmount is required when amountType is 'fixed'.");
    }
}

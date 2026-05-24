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
        var result = await paymentService.UpsertRecurringAsync(propertyId, req, ct);
        await SendOkAsync(result, ct);
    }
}

public class RecurringPaymentValidator : Validator<RecurringPaymentRequest>
{
    public RecurringPaymentValidator()
    {
        RuleFor(x => x.AmountType).NotEmpty();
        RuleFor(x => x.DraftDay).InclusiveBetween(1, 28);
        RuleFor(x => x.Method).NotEmpty().Must(m => m is "ach" or "card");
        RuleFor(x => x.FixedAmount).NotNull().GreaterThan(0)
            .When(x => x.AmountType?.Equals("fixed", StringComparison.OrdinalIgnoreCase) == true)
            .WithMessage("fixedAmount is required when amountType is 'fixed'.");
    }
}

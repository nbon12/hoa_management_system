using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Payments.Models;
using Microsoft.Extensions.Logging;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments.Recurring;

public class RecurringUpsertEndpoint(ResidentAccess access, PaymentService paymentService) : Endpoint<RecurringPaymentRequest, RecurringPaymentDto>
{
    public override void Configure()
    {
        Put("/payments/recurring");
        // 006 FR-028: autopay enrollment calls Stripe, so it is throttled like the other setup endpoints.
        Description(x => x.WithName("UpsertRecurring").WithTags("Payments").RequireRateLimiting("payments"));
    }

    public override async Task HandleAsync(RecurringPaymentRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Write, ct);
        var propertyId = home.PropertyId;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();
        var result = await paymentService.UpsertRecurringAsync(propertyId, home.UserId, req, ip, userAgent, ct);
        // Audit trail (FR-029): schedule-config change recorded with non-PII identifiers only.
        Logger.LogInformation(
            "audit payments.recurring.upsert property {PropertyId} amountType {AmountType} draftDay {DraftDay}",
            propertyId, req.AmountType, req.DraftDay);
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

using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments;

public class OneTimePaymentEndpoint(PaymentService paymentService) : Endpoint<OneTimePaymentRequest, OneTimePaymentResponse>
{
    public override void Configure()
    {
        Post("/payments/one-time");
        Description(x => x.WithName("SubmitOneTimePayment").WithTags("Payments").RequireRateLimiting("payments"));
    }

    public override async Task HandleAsync(OneTimePaymentRequest req, CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        try
        {
            var result = await paymentService.SubmitOneTimePaymentAsync(propertyId, req, ct);
            await SendOkAsync(result, ct);
        }
        catch (HOAManagementCompany.Features.Auth.DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

public class OneTimePaymentValidator : Validator<OneTimePaymentRequest>
{
    public OneTimePaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).NotEmpty().Must(m => m is "ach" or "card")
            .WithMessage("Method must be 'ach' or 'card'.");

        When(x => x.Method == "ach", () =>
        {
            RuleFor(x => x.RoutingNumber).NotEmpty().Length(9).Matches(@"^\d{9}$");
            RuleFor(x => x.AccountNumber).NotEmpty();
            RuleFor(x => x.AccountType).NotEmpty().Must(t => t is "checking" or "savings");
        });

        When(x => x.Method == "card", () =>
        {
            RuleFor(x => x.CardNumber).NotEmpty();
            RuleFor(x => x.CardExpiry).NotEmpty().Matches(@"^\d{2}/\d{2}$").WithMessage("Card expiry must be in MM/YY format.");
            RuleFor(x => x.CardCvv).NotEmpty();
            RuleFor(x => x.CardholderName).NotEmpty();
        });
    }
}

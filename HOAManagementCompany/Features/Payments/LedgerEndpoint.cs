using HOAManagementCompany.Features.Common;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments;

public class LedgerEndpoint(PaymentService paymentService) : Endpoint<LedgerRequest, LedgerResponse>
{
    public override void Configure()
    {
        Get("/payments/ledger");
        Description(x => x.WithName("GetLedger").WithTags("Payments"));
    }

    public override async Task HandleAsync(LedgerRequest req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var result = await paymentService.GetLedgerAsync(propertyId, req, ct);
        await SendOkAsync(result, ct);
    }
}

public class LedgerValidator : Validator<LedgerRequest>
{
    public LedgerValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments;

public class LedgerEndpoint(ResidentAccess access, PaymentService paymentService) : Endpoint<LedgerRequest, LedgerResponse>
{
    public override void Configure()
    {
        Get("/payments/ledger");
        Description(x => x.WithName("GetLedger").WithTags("Payments"));
    }

    public override async Task HandleAsync(LedgerRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.ReadHistory, ct);
        var propertyId = home.PropertyId;
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

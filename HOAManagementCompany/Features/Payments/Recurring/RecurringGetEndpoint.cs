using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments.Recurring;

public class RecurringGetEndpoint(ResidentAccess access, PaymentService paymentService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/payments/recurring");
        Description(x => x.WithName("GetRecurring").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var propertyId = home.PropertyId;
        var result = await paymentService.GetRecurringAsync(propertyId, ct);

        if (result is null)
            await SendNoContentAsync(ct);
        else
            await SendOkAsync(result, ct);
    }
}

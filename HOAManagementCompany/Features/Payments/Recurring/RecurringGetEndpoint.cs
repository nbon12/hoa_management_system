using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments.Recurring;

public class RecurringGetEndpoint(PaymentService paymentService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/payments/recurring");
        Description(x => x.WithName("GetRecurring").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        var result = await paymentService.GetRecurringAsync(propertyId, ct);

        if (result is null)
            await SendNoContentAsync(ct);
        else
            await SendOkAsync(result, ct);
    }
}

using FastEndpoints;

namespace HOAManagementCompany.Features.Payments.Recurring;

public class RecurringDeleteEndpoint(PaymentService paymentService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/payments/recurring");
        Description(x => x.WithName("CancelRecurring").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        await paymentService.CancelRecurringAsync(propertyId, ct);
        await SendNoContentAsync(ct);
    }
}

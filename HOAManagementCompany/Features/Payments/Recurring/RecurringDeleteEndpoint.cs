using FastEndpoints;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments.Recurring;

public class RecurringDeleteEndpoint(ResidentAccess access, PaymentService paymentService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/payments/recurring");
        Description(x => x.WithName("CancelRecurring").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Write, ct);
        var propertyId = home.PropertyId;
        await paymentService.CancelRecurringAsync(propertyId, ct);
        await SendNoContentAsync(ct);
    }
}

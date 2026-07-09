using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments;

public class DraftsEndpoint(PaymentService paymentService) : Endpoint<DraftsRequest, DraftsResponse>
{
    public override void Configure()
    {
        Get("/payments/drafts");
        Description(x => x.WithName("GetDrafts").WithTags("Payments"));
    }

    public override async Task HandleAsync(DraftsRequest req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var result = await paymentService.GetDraftsAsync(propertyId, req, ct);
        await SendOkAsync(result, ct);
    }
}

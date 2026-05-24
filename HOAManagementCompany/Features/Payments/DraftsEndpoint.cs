using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments;

public class DraftsEndpoint(PaymentService paymentService) : EndpointWithoutRequest<IEnumerable<DraftEntryDto>>
{
    public override void Configure()
    {
        Get("/payments/drafts");
        Description(x => x.WithName("GetDrafts").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        var result = await paymentService.GetDraftsAsync(propertyId, ct);
        await SendOkAsync(result, ct);
    }
}

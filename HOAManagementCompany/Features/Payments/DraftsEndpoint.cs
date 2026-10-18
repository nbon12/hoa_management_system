using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments;

public class DraftsEndpoint(ResidentAccess access, PaymentService paymentService) : Endpoint<DraftsRequest, DraftsResponse>
{
    public override void Configure()
    {
        Get("/payments/drafts");
        Description(x => x.WithName("GetDrafts").WithTags("Payments"));
    }

    public override async Task HandleAsync(DraftsRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var propertyId = home.PropertyId;
        var result = await paymentService.GetDraftsAsync(propertyId, req, ct);
        await SendOkAsync(result, ct);
    }
}

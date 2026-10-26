using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Property.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Property;

public class AddressHistoryEndpoint(ResidentAccess access, PropertyService propertyService) : EndpointWithoutRequest<IEnumerable<AddressHistoryDto>>
{
    public override void Configure()
    {
        Get("/property/address-history");
        Description(x => x.WithName("GetAddressHistory").WithTags("Property"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var propertyId = home.PropertyId;
        await SendOkAsync(await propertyService.GetAddressHistoryAsync(propertyId, ct), ct);
    }
}

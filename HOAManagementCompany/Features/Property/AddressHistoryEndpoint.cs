using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Property.Models;

namespace HOAManagementCompany.Features.Property;

public class AddressHistoryEndpoint(PropertyService propertyService) : EndpointWithoutRequest<IEnumerable<AddressHistoryDto>>
{
    public override void Configure()
    {
        Get("/property/address-history");
        Description(x => x.WithName("GetAddressHistory").WithTags("Property"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.RequirePropertyId();
        await SendOkAsync(await propertyService.GetAddressHistoryAsync(propertyId, ct), ct);
    }
}

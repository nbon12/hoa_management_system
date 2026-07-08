using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Property.Models;

namespace HOAManagementCompany.Features.Property;

public class OwnerGetEndpoint(PropertyService propertyService) : EndpointWithoutRequest<OwnerDto>
{
    public override void Configure()
    {
        Get("/property/owner");
        Description(x => x.WithName("GetOwner").WithTags("Property"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        await SendOkAsync(await propertyService.GetOwnerAsync(propertyId, ct), ct);
    }
}

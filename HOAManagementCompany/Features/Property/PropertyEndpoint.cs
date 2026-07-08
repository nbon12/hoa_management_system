using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Property.Models;

namespace HOAManagementCompany.Features.Property;

public class PropertyEndpoint(PropertyService propertyService) : EndpointWithoutRequest<PropertyDto>
{
    public override void Configure()
    {
        Get("/property");
        Description(x => x.WithName("GetProperty").WithTags("Property"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        await SendOkAsync(await propertyService.GetPropertyAsync(propertyId, ct), ct);
    }
}

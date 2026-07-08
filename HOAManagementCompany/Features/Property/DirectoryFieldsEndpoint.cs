using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Property.Models;

namespace HOAManagementCompany.Features.Property;

public class DirectoryFieldsEndpoint(PropertyService propertyService) : EndpointWithoutRequest<IEnumerable<DirectoryFieldDto>>
{
    public override void Configure()
    {
        Get("/property/directory-fields");
        Description(x => x.WithName("GetDirectoryFields").WithTags("Property"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        await SendOkAsync(await propertyService.GetDirectoryFieldsAsync(propertyId, ct), ct);
    }
}

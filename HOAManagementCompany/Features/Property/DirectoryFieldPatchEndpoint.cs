using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Property.Models;

namespace HOAManagementCompany.Features.Property;

public class DirectoryFieldPatchRequest2
{
    public string Key { get; set; } = string.Empty;
    public bool Shared { get; set; }
}

public class DirectoryFieldPatchEndpoint(PropertyService propertyService) : Endpoint<DirectoryFieldPatchRequest2, DirectoryFieldDto>
{
    public override void Configure()
    {
        Patch("/property/directory-fields/{key}");
        Description(x => x.WithName("PatchDirectoryField").WithTags("Property"));
    }

    public override async Task HandleAsync(DirectoryFieldPatchRequest2 req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var key = Route<string>("key")!;
        var result = await propertyService.PatchDirectoryFieldAsync(propertyId, key, new DirectoryFieldPatchRequest(req.Shared), ct);
        await SendOkAsync(result, ct);
    }
}

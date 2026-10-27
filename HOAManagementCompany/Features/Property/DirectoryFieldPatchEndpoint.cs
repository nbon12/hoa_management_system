using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Property.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Property;

public class DirectoryFieldPatchRequest2
{
    public string Key { get; set; } = string.Empty;
    public bool Shared { get; set; }
}

public class DirectoryFieldPatchEndpoint(ResidentAccess access, PropertyService propertyService) : Endpoint<DirectoryFieldPatchRequest2, DirectoryFieldDto>
{
    public override void Configure()
    {
        Patch("/property/directory-fields/{key}");
        Description(x => x.WithName("PatchDirectoryField").WithTags("Property"));
    }

    public override async Task HandleAsync(DirectoryFieldPatchRequest2 req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Write, ct);
        var propertyId = home.PropertyId;
        var key = Route<string>("key")!;
        try
        {
            var result = await propertyService.PatchDirectoryFieldAsync(propertyId, key, new DirectoryFieldPatchRequest(req.Shared), ct);
            await SendOkAsync(result, ct);
        }
        catch (DomainException ex)
        {
            HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
        }
    }
}

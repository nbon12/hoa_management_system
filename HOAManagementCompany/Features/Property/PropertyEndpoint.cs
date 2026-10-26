using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Property.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Property;

public class PropertyEndpoint(ResidentAccess access, PropertyService propertyService) : EndpointWithoutRequest<PropertyDto>
{
    public override void Configure()
    {
        Get("/property");
        Description(x => x.WithName("GetProperty").WithTags("Property"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var propertyId = home.PropertyId;
        try { await SendOkAsync(await propertyService.GetPropertyAsync(propertyId, ct), ct); }
        catch (DomainException ex) { HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct); }
    }
}

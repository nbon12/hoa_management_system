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
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        try { await SendOkAsync(await propertyService.GetOwnerAsync(propertyId, ct), ct); }
        catch (DomainException ex) { HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct); }
    }
}

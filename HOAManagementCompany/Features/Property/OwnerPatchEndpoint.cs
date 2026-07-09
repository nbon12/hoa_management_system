using HOAManagementCompany.Features.Common;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Property.Models;

namespace HOAManagementCompany.Features.Property;

public class OwnerPatchEndpoint(PropertyService propertyService) : Endpoint<OwnerPatchRequest, OwnerDto>
{
    public override void Configure()
    {
        Patch("/property/owner");
        Description(x => x.WithName("PatchOwner").WithTags("Property"));
    }

    public override async Task HandleAsync(OwnerPatchRequest req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        await SendOkAsync(await propertyService.PatchOwnerAsync(propertyId, req, ct), ct);
    }
}

public class OwnerPatchValidator : Validator<OwnerPatchRequest>
{
    public OwnerPatchValidator()
    {
        When(x => x.Email is not null, () => RuleFor(x => x.Email!).EmailAddress());
    }
}

using FastEndpoints;

namespace HOAManagementCompany.Features.Property.Architectural;

// Resident draft endpoints (029 contract §Drafts). Authorization is ResidentArcScope inside the
// service: the caller's active property only, a non-disclosing 403 otherwise.

/// <summary>POST /property/architectural-applications/drafts — start a draft (US1).</summary>
public class CreateDraftEndpoint(ResidentArcDraftService drafts) : Endpoint<ResidentArcDraftBody, ResidentArcDraftDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/drafts");
        Description(x => x.WithName("CreateArchitecturalDraft").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcDraftBody req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await SendAsync(await drafts.CreateAsync(User, req, ct), StatusCodes.Status201Created, ct);
    }
}

/// <summary>GET /property/architectural-applications/drafts/{draftId}.</summary>
public class GetDraftEndpoint(ResidentArcDraftService drafts) : Endpoint<ResidentArcDraftRoute, ResidentArcDraftDto>
{
    public override void Configure()
    {
        Get($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}");
        Description(x => x.WithName("GetArchitecturalDraft").WithTags(ResidentArcHttp.Tag));
    }

    public override async Task HandleAsync(ResidentArcDraftRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await SendOkAsync(await drafts.GetAsync(User, req.DraftId, ct), ct);
    }
}

/// <summary>PUT /property/architectural-applications/drafts/{draftId} — replace the editable fields.</summary>
public class UpdateDraftEndpoint(ResidentArcDraftService drafts) : Endpoint<ResidentArcDraftUpdateRequest, ResidentArcDraftDto>
{
    public override void Configure()
    {
        Put($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}");
        Description(x => x.WithName("UpdateArchitecturalDraft").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcDraftUpdateRequest req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await SendOkAsync(await drafts.UpdateAsync(User, req, ct), ct);
    }
}

/// <summary>DELETE /property/architectural-applications/drafts/{draftId} — discard, deleting its uploads (FR-014).</summary>
public class DeleteDraftEndpoint(ResidentArcDraftService drafts) : Endpoint<ResidentArcDraftRoute>
{
    public override void Configure()
    {
        Delete($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}");
        Description(x => x.WithName("DeleteArchitecturalDraft").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcDraftRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await drafts.DeleteAsync(User, req.DraftId, ct);
        await SendNoContentAsync(ct);
    }
}

/// <summary>POST /property/architectural-applications/drafts/{draftId}/submit — becomes ARC-&lt;n&gt; (US1, US6).</summary>
public class SubmitDraftEndpoint(ResidentArcSubmitService submit, ResidentArcQueries queries)
    : EndpointWithoutRequest<ResidentArcDetailDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}/submit");
        Description(x => x.WithName("SubmitArchitecturalDraft").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    // Bodiless POST: the draft holds the data, so only the route value is read.
    public override async Task HandleAsync(CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var applicationId = await submit.SubmitAsync(User, Route<Guid>("draftId"), ct);
        await SendAsync(await queries.DetailAsync(User, applicationId, ct), StatusCodes.Status201Created, ct);
    }
}

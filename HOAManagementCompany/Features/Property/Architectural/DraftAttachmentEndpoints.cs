using FastEndpoints;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Property.Architectural;

/// <summary>
/// POST /property/architectural-applications/drafts/{draftId}/attachments — one file, judged by its
/// bytes and the environment limits (US5, FR-010/FR-011). Stored privately; never a public URL.
/// </summary>
public class UploadDraftAttachmentEndpoint(ResidentArcDraftService drafts, IOptions<ArcUploadOptions> limits)
    : Endpoint<ResidentArcDraftRoute, ResidentArcAttachmentDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}/attachments");
        AllowFileUploads(dontAutoBindFormData: true);
        Description(x => x.WithName("UploadArchitecturalDraftAttachment").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcDraftRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var file = await ResidentArcHttp.ReadFileAsync(HttpContext, limits.Value, ct);
        await using var stream = file.OpenReadStream();
        var dto = await drafts.UploadAsync(User, req.DraftId, file.FileName, file.Length, stream, ct);
        await SendAsync(dto, StatusCodes.Status201Created, ct);
    }
}

/// <summary>DELETE …/drafts/{draftId}/attachments/{attachmentId} — remove one upload and its object.</summary>
public class DeleteDraftAttachmentEndpoint(ResidentArcDraftService drafts) : Endpoint<ResidentArcDraftAttachmentRoute>
{
    public override void Configure()
    {
        Delete($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}/attachments/{{attachmentId:guid}}");
        Description(x => x.WithName("DeleteArchitecturalDraftAttachment").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcDraftAttachmentRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await drafts.DeleteAttachmentAsync(User, req.DraftId, req.AttachmentId, ct);
        await SendNoContentAsync(ct);
    }
}

/// <summary>GET …/drafts/{draftId}/attachments/{attachmentId}/url — a short-lived link to an own or carried file.</summary>
public class DraftAttachmentUrlEndpoint(
    ResidentArcDraftService drafts, IDocumentStorage storage, TimeProvider clock, ILogger<DraftAttachmentUrlEndpoint> logger)
    : Endpoint<ResidentArcDraftAttachmentRoute, ResidentArcAttachmentUrlDto>
{
    public override void Configure()
    {
        Get($"{ResidentArcHttp.Base}/drafts/{{draftId:guid}}/attachments/{{attachmentId:guid}}/url");
        Description(x => x.WithName("GetArchitecturalDraftAttachmentUrl").WithTags(ResidentArcHttp.Tag));
    }

    public override async Task HandleAsync(ResidentArcDraftAttachmentRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var key = await drafts.AttachmentKeyAsync(User, req.DraftId, req.AttachmentId, ct);
        ResidentArcLog.AttachmentAccess(logger, ResidentArcScope.UserId(User), ResidentArcScope.PropertyId(User),
            $"attachment:{req.AttachmentId}", clock.GetUtcNow());
        await SendOkAsync(await ResidentArcHttp.LinkAsync(storage, clock, key, ct), ct);
    }
}

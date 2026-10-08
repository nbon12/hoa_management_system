using FastEndpoints;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Property.Architectural;

// Resident application endpoints (029 contract §Applications). There is deliberately no update or
// delete route for a submitted application: its content is immutable to the resident (FR-008).

/// <summary>GET /property/architectural-applications — "My architectural requests" (US2).</summary>
public class MyApplicationsListEndpoint(ResidentArcQueries queries) : Endpoint<ResidentArcListQuery, ResidentArcListResponse>
{
    public override void Configure()
    {
        Get(ResidentArcHttp.Base);
        Description(x => x.WithName("ListMyArchitecturalRequests").WithTags(ResidentArcHttp.Tag));
    }

    public override async Task HandleAsync(ResidentArcListQuery req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await SendOkAsync(await queries.ListAsync(User, req.Limit, req.Offset, ct), ct);
    }
}

/// <summary>GET /property/architectural-applications/{id} — resident-safe detail (US2).</summary>
public class MyApplicationDetailEndpoint(ResidentArcQueries queries) : Endpoint<ResidentArcRoute, ResidentArcDetailDto>
{
    public override void Configure()
    {
        Get($"{ResidentArcHttp.Base}/{{id:guid}}");
        Description(x => x.WithName("GetMyArchitecturalRequest").WithTags(ResidentArcHttp.Tag));
    }

    public override async Task HandleAsync(ResidentArcRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await SendOkAsync(await queries.DetailAsync(User, req.Id, ct), ct);
    }
}

/// <summary>GET …/{id}/attachments/{attachmentId}/url — a short-lived link (FR-013).</summary>
public class ApplicationAttachmentUrlEndpoint(
    ResidentArcActionsService actions, IDocumentStorage storage, TimeProvider clock, ILogger<ApplicationAttachmentUrlEndpoint> logger)
    : Endpoint<ResidentArcAttachmentRoute, ResidentArcAttachmentUrlDto>
{
    public override void Configure()
    {
        Get($"{ResidentArcHttp.Base}/{{id:guid}}/attachments/{{attachmentId:guid}}/url");
        Description(x => x.WithName("GetMyArchitecturalAttachmentUrl").WithTags(ResidentArcHttp.Tag));
    }

    public override async Task HandleAsync(ResidentArcAttachmentRoute req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var key = await actions.AttachmentKeyAsync(User, req.Id, req.AttachmentId, ct);
        ResidentArcLog.AttachmentAccess(logger, ResidentArcScope.UserId(User), ResidentArcScope.PropertyId(User),
            $"attachment:{req.AttachmentId}", clock.GetUtcNow());
        await SendOkAsync(await ResidentArcHttp.LinkAsync(storage, clock, key, ct), ct);
    }
}

/// <summary>POST …/{id}/info-requests/{infoRequestId}/attachments — a file for an info-request reply (US3).</summary>
public class UploadReplyAttachmentEndpoint(ResidentArcActionsService actions, IOptions<ArcUploadOptions> limits)
    : Endpoint<ResidentArcReplyUploadRequest, ResidentArcAttachmentDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/{{id:guid}}/info-requests/{{infoRequestId:guid}}/attachments");
        AllowFileUploads(dontAutoBindFormData: true);
        Description(x => x.WithName("UploadArchitecturalReplyAttachment").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcReplyUploadRequest req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var file = await ResidentArcHttp.ReadFileAsync(HttpContext, limits.Value, ct);
        await using var stream = file.OpenReadStream();
        var dto = await actions.UploadReplyAttachmentAsync(User, req.Id, req.InfoRequestId, file.FileName, file.Length, stream, ct);
        await SendAsync(dto, StatusCodes.Status201Created, ct);
    }
}

/// <summary>POST …/{id}/info-requests/{infoRequestId}/reply — answer the board's question (US3, FR-018).</summary>
public class ReplyInfoRequestEndpoint(ResidentArcActionsService actions, ResidentArcQueries queries)
    : Endpoint<ResidentArcReplyRequest, ResidentArcDetailDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/{{id:guid}}/info-requests/{{infoRequestId:guid}}/reply");
        Description(x => x.WithName("ReplyToArchitecturalInfoRequest").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(ResidentArcReplyRequest req, CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var app = await actions.ReplyAsync(User, req.Id, req.InfoRequestId, req.ResponseMessage, ct);
        await SendOkAsync(await queries.BuildDetailAsync(app, ct), ct);
    }
}

/// <summary>POST …/{id}/withdraw — withdraw an undecided request (US4, FR-020/FR-021).</summary>
public class WithdrawApplicationEndpoint(ResidentArcActionsService actions, ResidentArcQueries queries)
    : EndpointWithoutRequest<ResidentArcDetailDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/{{id:guid}}/withdraw");
        Description(x => x.WithName("WithdrawArchitecturalRequest").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        var app = await actions.WithdrawAsync(User, Route<Guid>("id"), ct);
        await SendOkAsync(await queries.BuildDetailAsync(app, ct), ct);
    }
}

/// <summary>POST …/{id}/revise — start a revise-and-resubmit draft from a closed denial (US6, FR-026).</summary>
public class ReviseApplicationEndpoint(ResidentArcDraftService drafts) : EndpointWithoutRequest<ResidentArcDraftDto>
{
    public override void Configure()
    {
        Post($"{ResidentArcHttp.Base}/{{id:guid}}/revise");
        Description(x => x.WithName("ReviseArchitecturalRequest").WithTags(ResidentArcHttp.Tag)
            .RequireRateLimiting(ResidentArcHttp.WritePolicy));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ResidentArcHttp.NoStore(HttpContext);
        await SendAsync(await drafts.CreateRevisionDraftAsync(User, Route<Guid>("id"), ct), StatusCodes.Status201Created, ct);
    }
}

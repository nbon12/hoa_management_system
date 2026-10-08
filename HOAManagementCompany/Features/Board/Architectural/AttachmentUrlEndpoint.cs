using FastEndpoints;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// GET …/{applicationId}/attachments/{attachmentId}/url — issues one short-lived pre-signed link
/// after the authorization check (027 FR-011/FR-012, 025 FR-039). Never returns a durable URL.
/// </summary>
public class AttachmentUrlEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcQueries queries,
    IDocumentStorage storage,
    ILogger<AttachmentUrlEndpoint> logger)
    : Endpoint<ArcAttachmentRoute, ArcAttachmentUrlDto>
{
    // S3DocumentStorage signs links for 5 minutes (within the spec's 15-minute cap).
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

    public override void Configure()
    {
        Get("/communities/{communityId}/architectural-applications/{applicationId}/attachments/{attachmentId}/url");
        Description(x => x.WithName("GetArchitecturalAttachmentUrl").WithTags("Board"));
    }

    public override async Task HandleAsync(ArcAttachmentRoute req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ViewArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var key = await db.ArchitecturalAttachments
            .Where(x => x.Id == req.AttachmentId
                        && x.ApplicationId == req.ApplicationId
                        && x.Application.CommunityId == req.CommunityId)
            .Select(x => x.StorageKey)
            .FirstOrDefaultAsync(ct);
        if (key is null)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        ArcLog.SensitiveAccess(logger, ArcQueries.CallerId(User), req.CommunityId, $"attachment:{req.AttachmentId}", queries.Now);

        if (!await storage.ExistsAsync(key, ct))
        {
            await ArcHttp.ErrorAsync(HttpContext, StatusCodes.Status404NotFound, ArcErrorCodes.AttachmentUnavailable,
                "This attachment is unavailable.", ct);
            return;
        }

        var url = await storage.GetPreSignedUrlAsync(key, ct);
        await SendOkAsync(new ArcAttachmentUrlDto(url, queries.Now.Add(LinkLifetime)), ct);
    }
}

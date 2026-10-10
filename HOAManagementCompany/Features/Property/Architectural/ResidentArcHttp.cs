using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;

namespace HOAManagementCompany.Features.Property.Architectural;

/// <summary>Shared HTTP plumbing for the resident architectural endpoints (029 contract).</summary>
internal static class ResidentArcHttp
{
    public const string Tag = "Architectural (resident)";
    public const string WritePolicy = "resident-writes";
    public const string Base = "/property/architectural-applications";

    // Signed links last 5 minutes (S3DocumentStorage), within the spec's 15-minute cap (FR-013).
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

    // Multipart framing on top of the file itself.
    private const long MultipartOverheadBytes = 1024 * 1024;

    /// <summary>Every resident ARC response is user-specific — never cached (constitution §8).</summary>
    public static void NoStore(HttpContext ctx) => ctx.Response.Headers.CacheControl = "no-store";

    /// <summary>
    /// Reads the single multipart field <c>file</c>. Raises the request-body limit to the configured
    /// per-file limit (plus framing) first. A body far beyond it is refused as FILE_TOO_LARGE, not a 500.
    /// </summary>
    public static async Task<IFormFile> ReadFileAsync(HttpContext ctx, ArcUploadOptions limits, CancellationToken ct)
    {
        var sizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = limits.MaxFileBytes + MultipartOverheadBytes;

        if (!ctx.Request.HasFormContentType)
            throw ResidentArcDraftService.Validation("Send the file as multipart/form-data in a field named 'file'.");

        IFormCollection form;
        try
        {
            form = await ctx.Request.ReadFormAsync(ct);
        }
        catch (Exception ex) when (ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
                                       or InvalidDataException)
        {
            throw new DomainException(ResidentArcErrorCodes.FileTooLarge,
                "The file is larger than the upload limit.", StatusCodes.Status422UnprocessableEntity);
        }

        return form.Files.GetFile("file")
               ?? throw ResidentArcDraftService.Validation("Send the file in a multipart field named 'file'.");
    }

    /// <summary>Issues one short-lived link after the caller's scope was checked; never a durable URL.</summary>
    public static async Task<ResidentArcAttachmentUrlDto> LinkAsync(
        IDocumentStorage storage, TimeProvider clock, string storageKey, CancellationToken ct)
    {
        if (!await storage.ExistsAsync(storageKey, ct))
            throw new DomainException(ResidentArcErrorCodes.AttachmentUnavailable,
                "This attachment is unavailable.", StatusCodes.Status404NotFound);
        var url = await storage.GetPreSignedUrlAsync(storageKey, ct);
        return new ResidentArcAttachmentUrlDto(url, clock.GetUtcNow().Add(LinkLifetime));
    }
}

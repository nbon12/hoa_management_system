using System.Text;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Property.Architectural;

// <!-- REPOWISE:START domain=resident-arc-uploads -->
// Judges a resident's architectural attachment by its bytes, never its name or declared type
// (029 FR-010): PDF, JPEG, PNG or HEIC only, identified by magic numbers. Enforces the
// environment-level limits from ArcUploadOptions (FR-011) before anything is stored.
// <!-- REPOWISE:END -->

/// <summary>Result of an attachment check: the sniffed content type, or an error code and message.</summary>
public sealed record ArcAttachmentCheck(string? ContentType, string? ErrorCode, string? Message)
{
    public static ArcAttachmentCheck Ok(string contentType) => new(contentType, null, null);
    public static ArcAttachmentCheck Fail(string code, string message) => new(null, code, message);
}

public sealed class ArcAttachmentValidator(IOptions<ArcUploadOptions> options)
{
    /// <summary>Bytes of the file header needed to recognise every allowed type.</summary>
    public const int HeaderLength = 16;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] FtypBox = "ftyp"u8.ToArray();

    // ISO-BMFF major brands used by HEIC/HEIF still images. Video brands (mp42, isom, …) are refused.
    private static readonly HashSet<string> HeifBrands = ["heic", "heix", "heim", "heis", "mif1", "heif"];

    public ArcUploadOptions Limits => options.Value;

    /// <summary>The canonical content type for the bytes, or null when they aren't an allowed type.</summary>
    public static string? Sniff(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfMagic)) return "application/pdf";
        if (header.StartsWith(JpegMagic)) return "image/jpeg";
        if (header.StartsWith(PngMagic)) return "image/png";
        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual(FtypBox)
            && HeifBrands.Contains(Encoding.ASCII.GetString(header.Slice(8, 4))))
            return "image/heic";
        return null;
    }

    /// <summary>
    /// Checks one new file of <paramref name="size"/> bytes against the per-file limit, the
    /// application's existing files (count and total, carried-over files included), and its content.
    /// </summary>
    public ArcAttachmentCheck Validate(long size, ReadOnlySpan<byte> header, int existingCount, long existingTotalBytes)
    {
        var limitFailure = CheckLimits(size, existingCount, existingTotalBytes);
        if (limitFailure is not null)
            return limitFailure;

        var type = size > 0 ? Sniff(header) : null;
        return type is null
            ? ArcAttachmentCheck.Fail(ResidentArcErrorCodes.UnsupportedFileType,
                "Only PDF, JPG, PNG and HEIC files can be attached.")
            : ArcAttachmentCheck.Ok(type);
    }

    /// <summary>
    /// The size, count and total checks alone — run on the declared length before the body is read,
    /// so an oversize upload is refused without buffering it. Null when within every limit.
    /// </summary>
    public ArcAttachmentCheck? CheckLimits(long size, int existingCount, long existingTotalBytes)
    {
        var limits = options.Value;
        if (size > limits.MaxFileBytes)
            return ArcAttachmentCheck.Fail(ResidentArcErrorCodes.FileTooLarge,
                $"Each file must be at most {Megabytes(limits.MaxFileBytes)}.");
        if (existingCount >= limits.MaxFilesPerApplication)
            return ArcAttachmentCheck.Fail(ResidentArcErrorCodes.AttachmentLimitReached,
                $"A request can have at most {limits.MaxFilesPerApplication} files.");
        if (existingTotalBytes + size > limits.MaxTotalBytes)
            return ArcAttachmentCheck.Fail(ResidentArcErrorCodes.AttachmentLimitReached,
                $"A request's files can total at most {Megabytes(limits.MaxTotalBytes)}.");
        return null;
    }

    private static string Megabytes(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)} MB" : $"{bytes} bytes";
}

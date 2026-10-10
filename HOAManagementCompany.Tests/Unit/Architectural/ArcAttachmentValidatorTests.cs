using System.Text;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Architectural;

/// <summary>
/// 029 T015 — attachments are judged by their bytes, not their name (FR-010), and the
/// environment-level size/count/total limits (FR-011) are enforced at their exact boundaries.
/// </summary>
public class ArcAttachmentValidatorTests
{
    // Small limits so the boundaries are easy to state: 100-byte files, 3 files, 250 bytes total.
    private static readonly ArcAttachmentValidator Validator = new(Options.Create(new ArcUploadOptions
    {
        MaxFileBytes = 100, MaxFilesPerApplication = 3, MaxTotalBytes = 250
    }));

    public static byte[] Pdf => Pad(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    public static byte[] Jpeg => Pad([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F']);
    public static byte[] Png => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);
    public static byte[] Heif(string brand) => Pad([0x00, 0x00, 0x00, 0x18, .. "ftyp"u8.ToArray(), .. Encoding.ASCII.GetBytes(brand), 0x00, 0x00, 0x00, 0x00]);

    private static byte[] Pad(byte[] header) => [.. header, .. new byte[Math.Max(0, 40 - header.Length)]];

    public static TheoryData<string, byte[], string> Allowed => new()
    {
        { "fence-plan.pdf", Pdf, "application/pdf" },
        { "elevation.jpg", Jpeg, "image/jpeg" },
        { "site.png", Png, "image/png" },
        { "IMG_0001.heic", Heif("heic"), "image/heic" },
        { "IMG_0002.heic", Heif("heix"), "image/heic" },
        { "IMG_0003.heic", Heif("mif1"), "image/heic" },
        { "IMG_0004.heif", Heif("heif"), "image/heic" },
        // The name never decides: a real PNG called .pdf is still accepted, as a PNG.
        { "misnamed.pdf", Png, "image/png" },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void AllowedContent_IsAccepted_WithItsCanonicalType(string fileName, byte[] bytes, string expectedType)
    {
        _ = fileName; // the validator never sees the name — that is the point
        var result = Validator.Validate(bytes.Length, bytes, existingCount: 0, existingTotalBytes: 0);

        Assert.Null(result.ErrorCode);
        Assert.Equal(expectedType, result.ContentType);
    }

    public static TheoryData<string, byte[]> Rejected => new()
    {
        { "plan.pdf (Windows executable)", Pad([(byte)'M', (byte)'Z', 0x90, 0x00, 0x03]) },
        { "photo.jpg (plain text)", Pad(Encoding.ASCII.GetBytes("this is not a photo")) },
        { "anim.gif", Pad(Encoding.ASCII.GetBytes("GIF89a")) },
        { "clip.heic (MP4 video brand)", Heif("mp42") },
        { "truncated png", [0x89, 0x50, 0x4E] },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void DisallowedContent_IsRejected_RegardlessOfName(string label, byte[] bytes)
    {
        _ = label;
        var result = Validator.Validate(bytes.Length, bytes, 0, 0);

        Assert.Equal("UNSUPPORTED_FILE_TYPE", result.ErrorCode);
        Assert.Null(result.ContentType);
    }

    [Fact]
    public void EmptyFile_IsRejected()
    {
        var result = Validator.Validate(0, [], 0, 0);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", result.ErrorCode);
    }

    // (size, existingCount, existingTotal, expectedError) against limits 100 / 3 / 250.
    [Theory]
    [InlineData(100, 0, 0, null)]                           // exactly the per-file limit
    [InlineData(101, 0, 0, "FILE_TOO_LARGE")]               // one byte over
    [InlineData(10, 2, 0, null)]                            // count = max - 1 before adding
    [InlineData(10, 3, 0, "ATTACHMENT_LIMIT_REACHED")]      // count already at max
    [InlineData(50, 0, 200, null)]                          // total lands exactly on the limit
    [InlineData(51, 0, 200, "ATTACHMENT_LIMIT_REACHED")]    // total one byte over
    public void Limits_AreEnforcedAtTheirBoundaries(long size, int existingCount, long existingTotal, string? expected)
    {
        var bytes = Pdf; // valid content, so only the limits can reject it
        var result = Validator.Validate(size, bytes, existingCount, existingTotal);

        Assert.Equal(expected, result.ErrorCode);
        if (expected is null)
            Assert.Equal("application/pdf", result.ContentType);
    }
}

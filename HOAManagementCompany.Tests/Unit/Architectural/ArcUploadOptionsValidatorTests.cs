using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Architectural;

/// <summary>
/// 029 T004 — the environment-level attachment limits (FR-011, Clarifications 2026-10-08) fail the
/// host fast when misconfigured, and the shipped defaults are 50 MB / 20 files / 250 MB.
/// </summary>
public class ArcUploadOptionsValidatorTests
{
    private static readonly ArcUploadOptionsValidator Validator = new();

    [Fact]
    public void Defaults_AreTheClarifiedLimits_AndValid()
    {
        var options = new ArcUploadOptions();

        Assert.Equal(52_428_800, options.MaxFileBytes);
        Assert.Equal(20, options.MaxFilesPerApplication);
        Assert.Equal(262_144_000, options.MaxTotalBytes);
        Assert.True(Validator.Validate(options).IsValid);
    }

    [Theory]
    [InlineData(0, 20, 1000, "MaxFileBytes")]
    [InlineData(-1, 20, 1000, "MaxFileBytes")]
    [InlineData(100, 0, 1000, "MaxFilesPerApplication")]
    [InlineData(100, -5, 1000, "MaxFilesPerApplication")]
    [InlineData(100, 20, 0, "MaxTotalBytes")]
    [InlineData(1001, 20, 1000, "MaxFileBytes must not exceed MaxTotalBytes")]
    public void InvalidLimits_AreRejected_WithTheOffendingSetting(long maxFile, int maxFiles, long maxTotal, string expected)
    {
        var result = Validator.Validate(new ArcUploadOptions
        {
            MaxFileBytes = maxFile, MaxFilesPerApplication = maxFiles, MaxTotalBytes = maxTotal
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains(expected));
    }

    [Fact]
    public void PerFileEqualToTotal_IsValid()
    {
        Assert.True(Validator.Validate(new ArcUploadOptions { MaxFileBytes = 10, MaxTotalBytes = 10 }).IsValid);
    }
}

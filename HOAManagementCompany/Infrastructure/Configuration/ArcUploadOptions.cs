// <!-- REPOWISE:START domain=configuration -->
// Environment-level limits for resident architectural-request attachments (029 FR-011,
// Clarifications 2026-10-08): per-file size, files per application, and total bytes per
// application. Bound from "Architectural:Uploads" and validated at startup.
// <!-- REPOWISE:END -->

using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>Attachment limits for resident architectural requests. Tunable per deployment.</summary>
public sealed class ArcUploadOptions
{
    public const string SectionName = "Architectural:Uploads";

    /// <summary>Largest single file accepted (default 50 MB).</summary>
    public long MaxFileBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Most files one application (or draft) may hold, carried-over files included (default 20).</summary>
    public int MaxFilesPerApplication { get; set; } = 20;

    /// <summary>Largest combined size of one application's files (default 250 MB).</summary>
    public long MaxTotalBytes { get; set; } = 250L * 1024 * 1024;
}

public sealed class ArcUploadOptionsValidator : AbstractValidator<ArcUploadOptions>
{
    public ArcUploadOptionsValidator()
    {
        RuleFor(x => x.MaxFileBytes).GreaterThan(0)
            .WithMessage("Architectural:Uploads:MaxFileBytes must be greater than 0.");
        RuleFor(x => x.MaxFilesPerApplication).GreaterThan(0)
            .WithMessage("Architectural:Uploads:MaxFilesPerApplication must be greater than 0.");
        RuleFor(x => x.MaxTotalBytes).GreaterThan(0)
            .WithMessage("Architectural:Uploads:MaxTotalBytes must be greater than 0.");
        RuleFor(x => x).Must(x => x.MaxFileBytes <= x.MaxTotalBytes)
            .WithName("MaxFileBytes")
            .WithMessage("Architectural:Uploads:MaxFileBytes must not exceed MaxTotalBytes.");
    }
}

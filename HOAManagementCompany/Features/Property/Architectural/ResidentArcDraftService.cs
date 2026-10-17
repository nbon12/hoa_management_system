using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Property.Architectural;

// <!-- REPOWISE:START domain=resident-arc -->
// Resident drafts (029 US1/US5/US6): create, read, edit and delete a draft; upload, remove and
// link its files; and start a revise-and-resubmit draft from a closed denial. Drafts live in
// ArchitecturalApplicationDrafts (research R3) and are invisible to the board until submitted.
// Every read and write goes through ResidentArcScope (the caller's active property only).
// <!-- REPOWISE:END -->

public sealed class ResidentArcDraftService(
    ApplicationDbContext db,
    ResidentArcScope scope,
    ArcAttachmentValidator validator,
    IDocumentStorage storage,
    TimeProvider clock,
    ILogger<ResidentArcDraftService> logger)
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxContractorLength = 200;

    public async Task<ResidentArcDraftDto> CreateAsync(ClaimsPrincipal user, ResidentArcDraftBody body, CancellationToken ct)
    {
        var propertyId = ResidentArcScope.PropertyId(user);
        var projectType = ValidateFields(body);
        var communityId = await db.Properties.Where(p => p.Id == propertyId)
            .Select(p => (Guid?)p.CommunityId).FirstOrDefaultAsync(ct)
            ?? throw ResidentArcScope.Forbidden();

        var now = clock.GetUtcNow();
        var draft = new ArchitecturalApplicationDraft
        {
            CommunityId = communityId,
            PropertyId = propertyId,
            CreatedByUserId = ResidentArcScope.UserId(user),
            CreatedAt = now
        };
        Apply(draft, body, projectType, now);
        db.ArchitecturalApplicationDrafts.Add(draft);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(draft, ct);
    }

    public async Task<ResidentArcDraftDto> GetAsync(ClaimsPrincipal user, Guid draftId, CancellationToken ct) =>
        await ToDtoAsync(await scope.DraftAsync(user, draftId, ct), ct);

    public async Task<ResidentArcDraftDto> UpdateAsync(ClaimsPrincipal user, ResidentArcDraftUpdateRequest req, CancellationToken ct)
    {
        var draft = await scope.DraftAsync(user, req.DraftId, ct);
        var projectType = ValidateFields(req);

        var removed = (req.RemovedCarriedAttachmentIds ?? []).Distinct().ToList();
        if (removed.Count > 0)
        {
            var carriedIds = await CarriedQuery(draft).Select(a => a.Id).ToListAsync(ct);
            if (removed.Any(id => !carriedIds.Contains(id)))
                throw Validation("removedCarriedAttachmentIds may only name files carried over from the previous revision.");
        }

        Apply(draft, req, projectType, clock.GetUtcNow());
        draft.RemovedCarriedAttachmentIds = removed;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(draft, ct);
    }

    /// <summary>Deletes the draft and the objects of its own uploads (FR-014). Carried-over objects stay.</summary>
    public async Task DeleteAsync(ClaimsPrincipal user, Guid draftId, CancellationToken ct)
    {
        var draft = await scope.DraftAsync(user, draftId, ct);
        foreach (var attachment in draft.Attachments)
            await storage.DeleteAsync(attachment.StorageKey, ct);
        db.ArchitecturalApplicationDrafts.Remove(draft);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Validates one uploaded file by content and against the environment limits, stores it, then
    /// records it. The object is written BEFORE the row, so a storage failure leaves nothing behind.
    /// </summary>
    public async Task<ResidentArcAttachmentDto> UploadAsync(
        ClaimsPrincipal user, Guid draftId, string fileName, long length, Stream content, CancellationToken ct)
    {
        var draft = await scope.DraftAsync(user, draftId, ct);
        var carried = await CarriedQuery(draft).Select(a => a.SizeBytes).ToListAsync(ct);
        var existingCount = draft.Attachments.Count + carried.Count;
        var existingTotal = draft.Attachments.Sum(a => a.SizeBytes) + carried.Sum();

        var bytes = await ReadCheckedAsync(user, draft.PropertyId, length, content, existingCount, existingTotal, ct);
        var key = $"arc/{draft.CommunityId}/drafts/{draft.Id}/{Guid.NewGuid()}";
        await UploadOrFailAsync(key, bytes.Bytes, bytes.ContentType, ct);

        var attachment = new ArchitecturalDraftAttachment
        {
            DraftId = draft.Id,
            FileName = CleanFileName(fileName),
            SizeBytes = bytes.Bytes.LongLength,
            ContentType = bytes.ContentType,
            StorageKey = key,
            UploadedByUserId = ResidentArcScope.UserId(user)
        };
        db.ArchitecturalDraftAttachments.Add(attachment);
        draft.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return new ResidentArcAttachmentDto(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.ContentType, null);
    }

    public async Task DeleteAttachmentAsync(ClaimsPrincipal user, Guid draftId, Guid attachmentId, CancellationToken ct)
    {
        var draft = await scope.DraftAsync(user, draftId, ct);
        var attachment = draft.Attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw ResidentArcScope.NotFound("The attachment");
        await storage.DeleteAsync(attachment.StorageKey, ct);
        db.ArchitecturalDraftAttachments.Remove(attachment);
        draft.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The storage key of one of the draft's own or carried-over attachments.</summary>
    public async Task<string> AttachmentKeyAsync(ClaimsPrincipal user, Guid draftId, Guid attachmentId, CancellationToken ct)
    {
        var draft = await scope.DraftAsync(user, draftId, ct);
        var own = draft.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (own is not null)
            return own.StorageKey;
        var carriedKey = await CarriedQuery(draft).Where(a => a.Id == attachmentId).Select(a => a.StorageKey).FirstOrDefaultAsync(ct);
        return carriedKey ?? throw ResidentArcScope.Forbidden();
    }

    /// <summary>
    /// Starts a revise-and-resubmit draft (029 US6) for the LATEST revision of a closed, denied
    /// application, pre-filled from it. Its files are carried, not copied. One revision draft at a time.
    /// </summary>
    public async Task<ResidentArcDraftDto> CreateRevisionDraftAsync(ClaimsPrincipal user, Guid applicationId, CancellationToken ct)
    {
        var app = await scope.ApplicationAsync(user, applicationId, ct);
        if (app.Status != ArcApplicationStatus.Closed || app.DecisionOutcome != ArcOutcome.Denied)
            throw RevisionNotAllowed("Only a closed, denied request can be revised and resubmitted.");

        var newer = await db.ArchitecturalApplications.AnyAsync(a =>
            a.CommunityId == app.CommunityId && a.ApplicationNumber == app.ApplicationNumber && a.Revision > app.Revision, ct);
        if (newer)
            throw RevisionNotAllowed("A newer revision of this request already exists.");
        if (await db.ArchitecturalApplicationDrafts.AnyAsync(d => d.PreviousRevisionId == app.Id, ct))
            throw RevisionNotAllowed("A revision of this request is already in progress.");

        var now = clock.GetUtcNow();
        var draft = new ArchitecturalApplicationDraft
        {
            CommunityId = app.CommunityId,
            PropertyId = app.PropertyId,
            CreatedByUserId = ResidentArcScope.UserId(user),
            PreviousRevisionId = app.Id,
            ProjectType = app.ProjectType,
            ProjectTitle = app.ProjectTitle,
            Description = app.Description,
            PlannedStartDate = app.PlannedStartDate,
            PlannedCompletionDate = app.PlannedCompletionDate,
            ContractorName = app.ContractorName,
            ContractorContact = app.ContractorContact,
            // A new submission needs a fresh acknowledgement.
            Acknowledged = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ArchitecturalApplicationDrafts.Add(draft);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The filtered unique index on PreviousRevisionId lost a race with a concurrent revise.
            throw RevisionNotAllowed("A revision of this request is already in progress.");
        }
        ResidentArcLog.RevisionDraftCreated(logger, ResidentArcScope.UserId(user), app.Id, draft.Id, now);
        return await ToDtoAsync(draft, ct);
    }

    // ── Shared with submit and info-reply uploads ────────────────────────────

    internal sealed record CheckedFile(byte[] Bytes, string ContentType);

    /// <summary>
    /// Refuses an over-limit length before reading, reads the file, and checks its content and limits.
    /// The stored content type is the sniffed one, never the client's declared type.
    /// </summary>
    internal async Task<CheckedFile> ReadCheckedAsync(
        ClaimsPrincipal user, Guid propertyId, long length, Stream content, int existingCount, long existingTotal, CancellationToken ct)
    {
        var precheck = validator.CheckLimits(length, existingCount, existingTotal);
        if (precheck is not null)
            throw Rejected(user, propertyId, precheck);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var check = validator.Validate(bytes.LongLength,
            bytes.AsSpan(0, Math.Min(bytes.Length, ArcAttachmentValidator.HeaderLength)), existingCount, existingTotal);
        if (check.ErrorCode is not null)
            throw Rejected(user, propertyId, check);
        return new CheckedFile(bytes, check.ContentType!);
    }

    internal async Task UploadOrFailAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        try
        {
            await storage.UploadAsync(key, bytes, contentType, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "ArcUploadStorageFailed: object storage rejected an architectural attachment upload");
            throw new DomainException(ResidentArcErrorCodes.StorageUnavailable,
                "File storage is temporarily unavailable. Please try again.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    internal static string CleanFileName(string? name)
    {
        var file = Path.GetFileName(name ?? string.Empty).Trim();
        if (file.Length == 0) file = "attachment";
        return file.Length <= 255 ? file : file[..255];
    }

    private DomainException Rejected(ClaimsPrincipal user, Guid propertyId, ArcAttachmentCheck check)
    {
        ResidentArcLog.UploadRejected(logger, ResidentArcScope.UserId(user), propertyId, check.ErrorCode!);
        return new DomainException(check.ErrorCode!, check.Message!, StatusCodes.Status422UnprocessableEntity);
    }

    /// <summary>Validates the editable fields (FR-002/FR-004 and length limits). Returns the project type.</summary>
    internal static ArcProjectType ValidateFields(ResidentArcDraftBody body)
    {
        if (!TryParseProjectType(body.ProjectType, out var projectType))
            throw Validation("projectType must be one of: " + string.Join(", ", Enum.GetNames<ArcProjectType>()) + ".");
        if (body.ProjectTitle is { Length: > MaxTitleLength })
            throw Validation($"projectTitle must be at most {MaxTitleLength} characters.");
        if (body.Description is { Length: > MaxDescriptionLength })
            throw Validation($"description must be at most {MaxDescriptionLength} characters.");
        if (body.ContractorName is { Length: > MaxContractorLength } || body.ContractorContact is { Length: > MaxContractorLength })
            throw Validation($"Contractor details must be at most {MaxContractorLength} characters each.");
        if (body.PlannedStartDate is { } start && body.PlannedCompletionDate is { } end && end < start)
            throw Validation("plannedCompletionDate can't be earlier than plannedStartDate.");
        return projectType;
    }

    internal static bool TryParseProjectType(string? value, out ArcProjectType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(value)
               && !int.TryParse(value, out _)
               && Enum.TryParse(value.Trim(), ignoreCase: true, out type)
               && Enum.IsDefined(type);
    }

    internal static DomainException Validation(string message) =>
        new(ResidentArcErrorCodes.ValidationError, message, StatusCodes.Status422UnprocessableEntity);

    private static DomainException RevisionNotAllowed(string message) =>
        new(ResidentArcErrorCodes.RevisionNotAllowed, message, StatusCodes.Status409Conflict);

    private static void Apply(ArchitecturalApplicationDraft draft, ResidentArcDraftBody body, ArcProjectType projectType, DateTimeOffset now)
    {
        draft.ProjectType = projectType;
        draft.ProjectTitle = body.ProjectTitle?.Trim() ?? string.Empty;
        draft.Description = body.Description?.Trim() ?? string.Empty;
        draft.PlannedStartDate = body.PlannedStartDate;
        draft.PlannedCompletionDate = body.PlannedCompletionDate;
        draft.ContractorName = Blank(body.ContractorName);
        draft.ContractorContact = Blank(body.ContractorContact);
        draft.Acknowledged = body.Acknowledged;
        draft.UpdatedAt = now;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>The previous revision's attachments still carried by a revision draft.</summary>
    internal IQueryable<ArchitecturalAttachment> CarriedQuery(ArchitecturalApplicationDraft draft)
    {
        if (draft.PreviousRevisionId is null)
            return db.ArchitecturalAttachments.Where(_ => false);
        var removed = draft.RemovedCarriedAttachmentIds;
        return db.ArchitecturalAttachments.Where(a =>
            a.ApplicationId == draft.PreviousRevisionId && !removed.Contains(a.Id));
    }

    private async Task<ResidentArcDraftDto> ToDtoAsync(ArchitecturalApplicationDraft draft, CancellationToken ct)
    {
        var carried = await CarriedQuery(draft)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ResidentArcAttachmentDto(a.Id, a.FileName, a.SizeBytes, a.ContentType, null))
            .ToListAsync(ct);
        string? previousDisplayId = null;
        if (draft.PreviousRevisionId is { } previousId)
            previousDisplayId = await db.ArchitecturalApplications.Where(a => a.Id == previousId)
                .Select(a => "ARC-" + a.ApplicationNumber).FirstOrDefaultAsync(ct);

        return new ResidentArcDraftDto(
            draft.Id,
            draft.ProjectType.ToString(),
            draft.ProjectTitle,
            draft.Description,
            draft.PlannedStartDate,
            draft.PlannedCompletionDate,
            draft.ContractorName,
            draft.ContractorContact,
            draft.Acknowledged,
            draft.PreviousRevisionId,
            previousDisplayId,
            draft.Attachments.OrderBy(a => a.CreatedAt)
                .Select(a => new ResidentArcAttachmentDto(a.Id, a.FileName, a.SizeBytes, a.ContentType, null)).ToList(),
            carried,
            draft.RemovedCarriedAttachmentIds,
            draft.UpdatedAt);
    }
}

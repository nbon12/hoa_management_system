using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Persistence;

using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Property.Architectural;

// <!-- REPOWISE:START domain=resident-arc -->
// Resident actions on a submitted request (029 US3/US4): reply to a board "Request info" (with
// optional files) and withdraw an undecided request. Both run under 027's ArcLocks row lock, so
// they can't race a deciding vote. Neither moves the due date, and withdrawal sends no email.
// <!-- REPOWISE:END -->

public sealed class ResidentArcActionsService(
    ApplicationDbContext db,
    ResidentArcScope scope,
    ResidentArcDraftService drafts,
    ArcQueries arcQueries,
    TimeProvider clock,
    ILogger<ResidentArcActionsService> logger)
{
    public const int MaxReplyLength = 2000;
    public const int MaxCommentLength = 2000;
    public const int MaxCommentFiles = 5;

    /// <summary>Withdraws an undecided request: Closed with outcome Withdrawn (FR-020/FR-021).</summary>
    public async Task<ArchitecturalApplication> WithdrawAsync(ClaimsPrincipal user, Guid applicationId, CancellationToken ct)
    {
        var app = await scope.ApplicationAsync(user, applicationId, ResidentOperation.Write, ct);
        var userId = ResidentArcScope.UserId(user);
        var now = clock.GetUtcNow();

        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            var blocked = OpenOnly(app);
            if (blocked is not null)
                return blocked;
            // 031: keep the board size at withdrawal so the closed tally doesn't drift.
            app.EligibleVoterCount = await arcQueries.EligibleCountAsync(app.CommunityId, app.PropertyId, ct);
            app.Status = ArcApplicationStatus.Closed;
            app.DecisionOutcome = ArcOutcome.Withdrawn;
            app.WithdrawnAt = now;
            app.WithdrawnByUserId = userId;
            app.ClosedAt = now;
            app.ClosedByUserId = userId;
            await db.SaveChangesAsync(ct);
            return null;
        }, ct);
        ThrowIfRefused(refusal);

        ResidentArcLog.Withdrawn(logger, userId, app.CommunityId, app.Id, now);
        return app;
    }

    /// <summary>Answers an outstanding info request and clears its marker (FR-018). The clock keeps running (FR-019).</summary>
    public async Task<ArchitecturalApplication> ReplyAsync(
        ClaimsPrincipal user, Guid applicationId, Guid infoRequestId, string? responseMessage, CancellationToken ct)
    {
        var message = responseMessage?.Trim();
        if (string.IsNullOrEmpty(message))
            throw ResidentArcDraftService.Validation("responseMessage is required.");
        if (message.Length > MaxReplyLength)
            throw ResidentArcDraftService.Validation($"responseMessage must be at most {MaxReplyLength} characters.");

        var app = await scope.ApplicationAsync(user, applicationId, ResidentOperation.Write, ct);
        var info = app.InfoRequests.FirstOrDefault(i => i.Id == infoRequestId)
                   ?? throw ResidentArcScope.NotFound("The information request");
        var userId = ResidentArcScope.UserId(user);
        var now = clock.GetUtcNow();

        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            await db.Entry(info).ReloadAsync(ct);
            var blocked = OpenOnly(app) ?? Unanswered(info);
            if (blocked is not null)
                return blocked;
            info.ResponseMessage = message;
            info.RespondedByUserId = userId;
            info.RespondedAt = now;
            await db.SaveChangesAsync(ct);
            return null;
        }, ct);
        ThrowIfRefused(refusal);

        ResidentArcLog.InfoReplied(logger, userId, app.Id, info.Id, now);
        return app;
    }

    /// <summary>Adds a file to an unanswered info request's reply (FR-018), with the same checks as draft uploads.</summary>
    public async Task<ResidentArcAttachmentDto> UploadReplyAttachmentAsync(
        ClaimsPrincipal user, Guid applicationId, Guid infoRequestId, string fileName, long length, Stream content, CancellationToken ct)
    {
        var app = await scope.ApplicationAsync(user, applicationId, ResidentOperation.Write, ct);
        var info = app.InfoRequests.FirstOrDefault(i => i.Id == infoRequestId)
                   ?? throw ResidentArcScope.NotFound("The information request");
        ThrowIfRefused(OpenOnly(app) ?? Unanswered(info));

        var file = await drafts.ReadCheckedAsync(user, app.PropertyId, length, content,
            app.Attachments.Count, app.Attachments.Sum(a => a.SizeBytes), ct);
        var key = $"arc/{app.CommunityId}/{app.ApplicationNumber}/{Guid.NewGuid()}";
        await drafts.UploadOrFailAsync(key, file.Bytes, file.ContentType, ct);

        var attachment = new ArchitecturalAttachment
        {
            ApplicationId = app.Id,
            InfoRequestId = info.Id,
            FileName = ResidentArcDraftService.CleanFileName(fileName),
            SizeBytes = file.Bytes.LongLength,
            ContentType = file.ContentType,
            StorageKey = key,
            UploadedByUserId = ResidentArcScope.UserId(user),
            CreatedAt = clock.GetUtcNow()
        };
        db.ArchitecturalAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        return new ResidentArcAttachmentDto(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.ContentType, info.Id);
    }

    /// <summary>
    /// 031: posts a homeowner comment with text and/or files on any of the caller's requests that wasn't
    /// withdrawn — open, decided or closed (e.g. to answer "revisions requested"). Files get the same
    /// type, size and per-application limits as every other upload.
    /// </summary>
    public async Task<ArchitecturalApplication> CommentAsync(
        ClaimsPrincipal user, Guid applicationId, string? message, IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        var body = message?.Trim() ?? string.Empty;
        if (body.Length == 0 && files.Count == 0)
            throw ResidentArcDraftService.Validation("Write a comment or attach a file.");
        if (body.Length > MaxCommentLength)
            throw ResidentArcDraftService.Validation($"message must be at most {MaxCommentLength} characters.");

        var app = await scope.ApplicationAsync(user, applicationId, ResidentOperation.Write, ct);
        if (app.DecisionOutcome == ArcOutcome.Withdrawn)
            throw new DomainException(ArcErrorCodes.ApplicationClosed, "This request was withdrawn.", StatusCodes.Status409Conflict);

        // Check every file before uploading any, so a rejected file leaves nothing behind.
        var count = app.Attachments.Count;
        var total = app.Attachments.Sum(a => a.SizeBytes);
        var checkedFiles = new List<(string Name, ResidentArcDraftService.CheckedFile File)>();
        foreach (var f in files)
        {
            await using var stream = f.OpenReadStream();
            var file = await drafts.ReadCheckedAsync(user, app.PropertyId, f.Length, stream, count, total, ct);
            checkedFiles.Add((f.FileName, file));
            count++;
            total += file.Bytes.LongLength;
        }

        var userId = ResidentArcScope.UserId(user);
        var now = clock.GetUtcNow();
        var comment = new ArchitecturalOwnerComment
        {
            Id = Guid.NewGuid(), ApplicationId = app.Id, AuthorUserId = userId, Body = body, CreatedAt = now
        };
        db.ArchitecturalOwnerComments.Add(comment);
        foreach (var (name, file) in checkedFiles)
        {
            var key = $"arc/{app.CommunityId}/{app.ApplicationNumber}/{Guid.NewGuid()}";
            await drafts.UploadOrFailAsync(key, file.Bytes, file.ContentType, ct);
            db.ArchitecturalAttachments.Add(new ArchitecturalAttachment
            {
                ApplicationId = app.Id,
                OwnerCommentId = comment.Id,
                FileName = ResidentArcDraftService.CleanFileName(name),
                SizeBytes = file.Bytes.LongLength,
                ContentType = file.ContentType,
                StorageKey = key,
                UploadedByUserId = userId,
                CreatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        ResidentArcLog.Commented(logger, userId, app.Id, comment.Id, checkedFiles.Count, now);
        return app;
    }

    /// <summary>The storage key of an attachment on the caller's application, or the non-disclosing 403.</summary>
    public async Task<(string Key, Guid PropertyId)> AttachmentKeyAsync(ClaimsPrincipal user, Guid applicationId, Guid attachmentId, CancellationToken ct)
    {
        var app = await scope.ApplicationAsync(user, applicationId, ResidentOperation.ReadHistory, ct);
        return (app.Attachments.FirstOrDefault(a => a.Id == attachmentId)?.StorageKey ?? throw ResidentArcScope.Forbidden(), app.PropertyId);
    }

    private static ArcRefusal? OpenOnly(ArchitecturalApplication app) => app.Status switch
    {
        ArcApplicationStatus.DecisionReached => new ArcRefusal(StatusCodes.Status409Conflict, ArcErrorCodes.ApplicationDecided,
            "The board has already reached a decision on this request."),
        ArcApplicationStatus.Closed => new ArcRefusal(StatusCodes.Status409Conflict, ArcErrorCodes.ApplicationClosed,
            "This request is closed."),
        _ => null
    };

    private static ArcRefusal? Unanswered(ArchitecturalInfoRequest info) =>
        info.RespondedAt is null
            ? null
            : new ArcRefusal(StatusCodes.Status409Conflict, ResidentArcErrorCodes.InfoAlreadyAnswered,
                "This question has already been answered.");

    private static void ThrowIfRefused(ArcRefusal? refusal)
    {
        if (refusal is not null)
            throw new DomainException(refusal.Code, refusal.Message, refusal.Status);
    }
}

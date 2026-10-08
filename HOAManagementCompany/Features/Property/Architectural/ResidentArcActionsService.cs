using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Persistence;

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
    TimeProvider clock,
    ILogger<ResidentArcActionsService> logger)
{
    public const int MaxReplyLength = 2000;

    /// <summary>Withdraws an undecided request: Closed with outcome Withdrawn (FR-020/FR-021).</summary>
    public async Task<ArchitecturalApplication> WithdrawAsync(ClaimsPrincipal user, Guid applicationId, CancellationToken ct)
    {
        var app = await scope.ApplicationAsync(user, applicationId, ct);
        var userId = ResidentArcScope.UserId(user);
        var now = clock.GetUtcNow();

        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            var blocked = OpenOnly(app);
            if (blocked is not null)
                return blocked;
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

        var app = await scope.ApplicationAsync(user, applicationId, ct);
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
        var app = await scope.ApplicationAsync(user, applicationId, ct);
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

    /// <summary>The storage key of an attachment on the caller's application, or the non-disclosing 403.</summary>
    public async Task<string> AttachmentKeyAsync(ClaimsPrincipal user, Guid applicationId, Guid attachmentId, CancellationToken ct)
    {
        var app = await scope.ApplicationAsync(user, applicationId, ct);
        return app.Attachments.FirstOrDefault(a => a.Id == attachmentId)?.StorageKey ?? throw ResidentArcScope.Forbidden();
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

using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Property.Architectural;

// <!-- REPOWISE:START domain=resident-arc -->
// Submits a resident draft (029 US1/US6). 027's ArcApplicationFactory allocates ARC-<n> atomically,
// sets received and due dates, snapshots the community rules, and links revisions. In the same
// transaction the draft is removed and the confirmation email is queued in the outbox, then
// dispatched after commit.
// <!-- REPOWISE:END -->

public sealed class ResidentArcSubmitService(
    ApplicationDbContext db,
    ResidentArcScope scope,
    ArcApplicationFactory factory,
    ArcEmailRenderer renderer,
    OutboxDispatcher dispatcher,
    TimeProvider clock,
    ILogger<ResidentArcSubmitService> logger)
{
    public static string SubmittedDedupKey(Guid applicationId) => $"arc:{applicationId}:submitted";

    public async Task<Guid> SubmitAsync(ClaimsPrincipal user, Guid draftId, CancellationToken ct)
    {
        var draft = await scope.DraftAsync(user, draftId, ct);
        ValidateForSubmit(draft);
        var userId = ResidentArcScope.UserId(user);

        ArchitecturalApplication? app = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var settings = await factory.GetOrCreateSettingsAsync(draft.CommunityId, ct);
            var now = clock.GetUtcNow();
            var received = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, ArcQueries.ResolveTimeZone(settings.TimeZoneId)).DateTime);

            var property = await db.Properties.Where(p => p.Id == draft.PropertyId)
                .Select(p => new { p.Address, CommunityName = p.Community.CommunityName })
                .FirstAsync(ct);
            var owner = await db.Owners.Where(o => o.PropertyId == draft.PropertyId)
                .Select(o => new { o.FirstName, o.LastName }).FirstOrDefaultAsync(ct);
            var submitter = await db.Users.Where(u => u.Id == userId)
                .Select(u => new { u.FirstName, u.LastName, u.Email }).FirstAsync(ct);
            var ownerName = owner is not null
                ? $"{owner.FirstName} {owner.LastName}".Trim()
                : $"{submitter.FirstName} {submitter.LastName}".Trim();

            var input = new ArcNewApplication(
                draft.PropertyId,
                ownerName,
                draft.ProjectType,
                draft.ProjectTitle,
                draft.Description,
                received,
                userId,
                draft.Attachments.Select(a => new ArcNewAttachment(a.FileName, a.SizeBytes, a.ContentType, a.StorageKey)).ToList(),
                draft.PlannedStartDate,
                draft.PlannedCompletionDate,
                draft.ContractorName,
                draft.ContractorContact,
                AcknowledgedAt: now);

            app = draft.PreviousRevisionId is { } previousId
                ? await factory.CreateRevisionAsync(previousId, input, draft.RemovedCarriedAttachmentIds, ct)
                : await factory.CreateFromSettingsAsync(input, ct);
            app.CreatedAt = now;

            // The resident's own uploads carry their uploader; carried-over rows keep none.
            var draftKeys = draft.Attachments.ToDictionary(a => a.StorageKey, a => a.UploadedByUserId);
            foreach (var attachment in app.Attachments)
                if (draftKeys.TryGetValue(attachment.StorageKey, out var uploader))
                    attachment.UploadedByUserId = uploader;

            // The draft becomes the application. Its objects stay: the new rows reference the same keys.
            db.ArchitecturalApplicationDrafts.Remove(draft);

            if (!string.IsNullOrWhiteSpace(submitter.Email))
            {
                var message = renderer.OwnerSubmitted(app, property.Address, submitter.FirstName, submitter.Email, property.CommunityName);
                db.OutboxMessages.Add(ArcEmailRenderer.ToOutbox(ArcEmailKinds.OwnerSubmitted, message,
                    SubmittedDedupKey(app.Id), ownerId: null, recipientUserId: userId));
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        ResidentArcLog.Submitted(logger, userId, app!.CommunityId, app.Id, app.Revision, clock.GetUtcNow());
        await dispatcher.DispatchPendingAsync(ct);
        return app.Id;
    }

    /// <summary>Required fields, date order and the acknowledgement (FR-001/FR-003/FR-004).</summary>
    private static void ValidateForSubmit(ArchitecturalApplicationDraft draft)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.ProjectTitle)) missing.Add("projectTitle");
        if (string.IsNullOrWhiteSpace(draft.Description)) missing.Add("description");
        if (draft.PlannedStartDate is null) missing.Add("plannedStartDate");
        if (draft.PlannedCompletionDate is null) missing.Add("plannedCompletionDate");
        if (missing.Count > 0)
            throw ResidentArcDraftService.Validation("Required to submit: " + string.Join(", ", missing) + ".");
        if (draft.PlannedCompletionDate < draft.PlannedStartDate)
            throw ResidentArcDraftService.Validation("plannedCompletionDate can't be earlier than plannedStartDate.");
        if (!draft.Acknowledged)
            throw new DomainException(ResidentArcErrorCodes.AcknowledgementRequired,
                "Confirm that work may not begin until this request is approved.", StatusCodes.Status422UnprocessableEntity);
    }
}

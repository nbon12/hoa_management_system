using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

public sealed record ArcNewAttachment(string FileName, long SizeBytes, string ContentType, string StorageKey);

public sealed record ArcNewApplication(
    Guid PropertyId,
    string OwnerName,
    ArcProjectType ProjectType,
    string ProjectTitle,
    string Description,
    DateOnly ReceivedDate,
    string? SubmittedByUserId,
    IReadOnlyList<ArcNewAttachment> Attachments);

/// <summary>
/// Creates architectural application rows (027 T063): allocates the community's next ARC number
/// atomically, computes the due date from the community's review period, snapshots the decision
/// rule, lapse rule and time zone (FR-029/FR-030), and creates revisions of a closed denial with
/// carried-over attachment metadata (FR-034/FR-035). Shared with the resident submission spec.
/// The caller owns SaveChanges for the returned row.
/// </summary>
public sealed class ArcApplicationFactory(ApplicationDbContext db)
{
    /// <summary>Returns the community's settings row, creating it with defaults when absent.</summary>
    public async Task<CommunityArcSettings> GetOrCreateSettingsAsync(Guid communityId, CancellationToken ct)
    {
        var settings = await db.CommunityArcSettings.FirstOrDefaultAsync(s => s.CommunityId == communityId, ct);
        if (settings is not null)
            return settings;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "CommunityArcSettings" ("CommunityId","ReviewPeriodDays","LapseRule","DecisionRule","ReminderDays","TimeZoneId","FormalDisapprovalStatement","NextApplicationNumber","UpdatedAt") VALUES ({communityId},{CommunityArcSettings.DefaultReviewPeriodDays},{ArcLapseRule.FlagOverdueOnly.ToString()},{ArcDecisionRule.MajorityOfMembers.ToString()},{CommunityArcSettings.DefaultReminderDays},{CommunityArcSettings.DefaultTimeZoneId},{CommunityArcSettings.DefaultFormalDisapprovalStatement},{CommunityArcSettings.FirstApplicationNumber},{DateTimeOffset.UtcNow}) ON CONFLICT ("CommunityId") DO NOTHING""",
            ct);
        return await db.CommunityArcSettings.FirstAsync(s => s.CommunityId == communityId, ct);
    }

    public async Task<ArchitecturalApplication> CreateFromSettingsAsync(ArcNewApplication input, CancellationToken ct)
    {
        var communityId = await db.Properties
            .Where(p => p.Id == input.PropertyId)
            .Select(p => (Guid?)p.CommunityId)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException(ArcErrorCodes.ValidationError, "Unknown property.", 422);

        var settings = await GetOrCreateSettingsAsync(communityId, ct);
        var number = await AllocateNumberAsync(communityId, ct);

        var app = NewRow(communityId, number, 1, null, input, settings);
        foreach (var a in input.Attachments)
            app.Attachments.Add(ToAttachment(a));

        db.ArchitecturalApplications.Add(app);
        return app;
    }

    /// <summary>
    /// Creates the next revision of a closed, denied application. Attachments carry over as new
    /// metadata rows sharing the same storage keys, minus any in <paramref name="removeAttachmentIds"/>.
    /// Earlier revisions are never modified.
    /// </summary>
    public async Task<ArchitecturalApplication> CreateRevisionAsync(
        Guid previousRevisionId,
        ArcNewApplication input,
        IReadOnlyCollection<Guid> removeAttachmentIds,
        CancellationToken ct)
    {
        var previous = await db.ArchitecturalApplications
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == previousRevisionId, ct)
            ?? throw new DomainException(ArcErrorCodes.ValidationError, "Unknown application.", 422);

        if (previous.Status != ArcApplicationStatus.Closed || previous.DecisionOutcome != ArcOutcome.Denied)
            throw new DomainException("REVISION_NOT_ALLOWED",
                "Only a closed, denied application can be revised and resubmitted.", 409);

        var newer = await db.ArchitecturalApplications.AnyAsync(a =>
            a.CommunityId == previous.CommunityId
            && a.ApplicationNumber == previous.ApplicationNumber
            && a.Revision > previous.Revision, ct);
        if (newer)
            throw new DomainException("REVISION_NOT_ALLOWED", "A newer revision already exists.", 409);

        var settings = await GetOrCreateSettingsAsync(previous.CommunityId, ct);
        var app = NewRow(previous.CommunityId, previous.ApplicationNumber, previous.Revision + 1, previous.Id,
            input with { PropertyId = previous.PropertyId }, settings);

        foreach (var carried in previous.Attachments.Where(a => !removeAttachmentIds.Contains(a.Id)))
            app.Attachments.Add(new ArchitecturalAttachment
            {
                FileName = carried.FileName,
                SizeBytes = carried.SizeBytes,
                ContentType = carried.ContentType,
                StorageKey = carried.StorageKey
            });
        foreach (var a in input.Attachments)
            app.Attachments.Add(ToAttachment(a));

        db.ArchitecturalApplications.Add(app);
        return app;
    }

    private async Task<int> AllocateNumberAsync(Guid communityId, CancellationToken ct)
    {
        // Atomic under concurrency: the row update serializes allocators (027 research R3).
        var allocated = await db.Database
            .SqlQuery<int>($"""UPDATE "CommunityArcSettings" SET "NextApplicationNumber" = "NextApplicationNumber" + 1 WHERE "CommunityId" = {communityId} RETURNING "NextApplicationNumber" - 1 AS "Value" """)
            .ToListAsync(ct);
        return allocated.Single();
    }

    private static ArchitecturalApplication NewRow(
        Guid communityId, int number, int revision, Guid? previousId, ArcNewApplication input, CommunityArcSettings settings) =>
        new()
        {
            CommunityId = communityId,
            PropertyId = input.PropertyId,
            ApplicationNumber = number,
            Revision = revision,
            PreviousRevisionId = previousId,
            SubmittedByUserId = input.SubmittedByUserId,
            OwnerName = input.OwnerName,
            ProjectType = input.ProjectType,
            ProjectTitle = input.ProjectTitle,
            Description = input.Description,
            ReceivedDate = input.ReceivedDate,
            DueDate = input.ReceivedDate.AddDays(settings.ReviewPeriodDays),
            DecisionRule = settings.DecisionRule,
            LapseRule = settings.LapseRule,
            TimeZoneId = settings.TimeZoneId,
            Status = ArcApplicationStatus.Open
        };

    private static ArchitecturalAttachment ToAttachment(ArcNewAttachment a) => new()
    {
        FileName = a.FileName,
        SizeBytes = a.SizeBytes,
        ContentType = a.ContentType,
        StorageKey = a.StorageKey
    };
}

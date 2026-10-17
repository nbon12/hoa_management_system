using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Property.Architectural;

// <!-- REPOWISE:START domain=resident-arc -->
// Resident-safe reads (029 US2): "My architectural requests" (drafts plus the latest revision of
// each application on the caller's active property) and the request detail. Status is projected
// from 027's state (data-model.md). Votes, voter identities and board vote comments are never
// loaded, so they can't leak into a resident response (FR-016).
// <!-- REPOWISE:END -->

public sealed class ResidentArcQueries(ApplicationDbContext db, ResidentArcScope scope)
{
    public const string KindDraft = "Draft";
    public const string KindApplication = "Application";

    /// <summary>Projects 027's lifecycle onto the resident status vocabulary.</summary>
    public static string ProjectStatus(ArcApplicationStatus status, ArcOutcome? outcome, bool hasUnansweredInfoRequest) =>
        status switch
        {
            ArcApplicationStatus.Closed => outcome switch
            {
                ArcOutcome.Approved => ResidentArcStatuses.Approved,
                ArcOutcome.Withdrawn => ResidentArcStatuses.Withdrawn,
                _ => ResidentArcStatuses.Denied
            },
            ArcApplicationStatus.Open when hasUnansweredInfoRequest => ResidentArcStatuses.MoreInfoRequested,
            // Open, or DecisionReached: the decision isn't official until the manager records it.
            _ => ResidentArcStatuses.Submitted
        };

    public async Task<ResidentArcListResponse> ListAsync(ClaimsPrincipal user, int? limit, int? offset, CancellationToken ct)
    {
        var propertyId = ResidentArcScope.PropertyId(user);
        var (take, skip) = Paging.Normalize(limit, offset);

        var drafts = await db.ArchitecturalApplicationDrafts
            .Where(d => d.PropertyId == propertyId)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.Id, d.ProjectType, d.ProjectTitle, d.UpdatedAt,
                Revision = d.PreviousRevision == null ? 1 : d.PreviousRevision.Revision + 1,
                Own = d.Attachments.Count,
                Carried = db.ArchitecturalAttachments.Count(a =>
                    a.ApplicationId == d.PreviousRevisionId && !d.RemovedCarriedAttachmentIds.Contains(a.Id))
            })
            .ToListAsync(ct);

        // Only the latest revision of each application number is listed.
        var apps = await db.ArchitecturalApplications
            .Where(a => a.PropertyId == propertyId
                        && !db.ArchitecturalApplications.Any(n =>
                            n.CommunityId == a.CommunityId && n.ApplicationNumber == a.ApplicationNumber && n.Revision > a.Revision))
            .OrderByDescending(a => a.ReceivedDate).ThenByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.ApplicationNumber, a.Revision, a.ProjectType, a.ProjectTitle, a.Status, a.DecisionOutcome,
                a.ReceivedDate, a.DueDate, a.CreatedAt, a.ClosedAt, a.WithdrawnAt,
                Attachments = a.Attachments.Count,
                Unanswered = a.InfoRequests.Any(i => i.RespondedAt == null),
                LastInfo = a.InfoRequests.Max(i => (DateTimeOffset?)(i.RespondedAt ?? i.RequestedAt))
            })
            .ToListAsync(ct);

        // A resident has a handful of requests, so the two lists are merged and paged in memory.
        var items = drafts
            .Select(d => new ResidentArcListItemDto(KindDraft, d.Id, null, d.Revision, d.ProjectType.ToString(), d.ProjectTitle,
                ResidentArcStatuses.Draft, d.Own + d.Carried, null, null, d.UpdatedAt))
            .Concat(apps.Select(a => new ResidentArcListItemDto(KindApplication, a.Id, $"ARC-{a.ApplicationNumber}", a.Revision,
                a.ProjectType.ToString(), a.ProjectTitle, ProjectStatus(a.Status, a.DecisionOutcome, a.Unanswered),
                a.Attachments, a.ReceivedDate, a.DueDate, Latest(a.CreatedAt, a.ClosedAt, a.WithdrawnAt, a.LastInfo))))
            .ToList();

        return new ResidentArcListResponse(items.Skip(skip).Take(take).ToList(), items.Count, take, skip);
    }

    public async Task<ResidentArcDetailDto> DetailAsync(ClaimsPrincipal user, Guid applicationId, CancellationToken ct) =>
        await BuildDetailAsync(await scope.ApplicationAsync(user, applicationId, ct), ct);

    public async Task<ResidentArcDetailDto> BuildDetailAsync(ArchitecturalApplication app, CancellationToken ct)
    {
        var unanswered = app.InfoRequests.Any(i => i.RespondedAt == null);
        var status = ProjectStatus(app.Status, app.DecisionOutcome, unanswered);
        var denied = app.Status == ArcApplicationStatus.Closed && app.DecisionOutcome == ArcOutcome.Denied;
        var approved = app.Status == ArcApplicationStatus.Closed && app.DecisionOutcome == ArcOutcome.Approved;

        string? statement = null;
        if (denied)
            statement = await db.CommunityArcSettings.Where(s => s.CommunityId == app.CommunityId)
                            .Select(s => s.FormalDisapprovalStatement).FirstOrDefaultAsync(ct)
                        ?? CommunityArcSettings.DefaultFormalDisapprovalStatement;

        var revisions = await db.ArchitecturalApplications
            .Where(a => a.CommunityId == app.CommunityId && a.ApplicationNumber == app.ApplicationNumber && a.PropertyId == app.PropertyId)
            .OrderBy(a => a.Revision)
            .Select(a => new { a.Id, a.Revision, a.Status, a.DecisionOutcome, a.ReceivedDate,
                Unanswered = a.InfoRequests.Any(i => i.RespondedAt == null) })
            .ToListAsync(ct);
        var isLatest = revisions.Count == 0 || revisions[^1].Id == app.Id;
        var canRevise = denied && isLatest
                        && !await db.ArchitecturalApplicationDrafts.AnyAsync(d => d.PreviousRevisionId == app.Id, ct);

        var infoRequests = app.InfoRequests.OrderBy(i => i.RequestedAt)
            .Select(i => new ResidentArcInfoRequestDto(i.Id, i.Message, i.RequestedAt, i.ResponseMessage, i.RespondedAt))
            .ToList();

        return new ResidentArcDetailDto(
            KindApplication,
            app.Id,
            app.DisplayId,
            app.Revision,
            app.ProjectType.ToString(),
            app.ProjectTitle,
            status,
            app.Attachments.Count,
            app.ReceivedDate,
            app.DueDate,
            Latest(app.CreatedAt, app.ClosedAt, app.WithdrawnAt,
                app.InfoRequests.Select(i => (DateTimeOffset?)(i.RespondedAt ?? i.RequestedAt)).Max()),
            app.Description,
            app.PlannedStartDate,
            app.PlannedCompletionDate,
            app.ContractorName,
            app.ContractorContact,
            app.AcknowledgedAt,
            app.Attachments.OrderBy(a => a.CreatedAt)
                .Select(a => new ResidentArcAttachmentDto(a.Id, a.FileName, a.SizeBytes, a.ContentType, a.InfoRequestId)).ToList(),
            infoRequests,
            Timeline(app),
            approved || denied
                ? new ResidentArcDecisionDto(app.DecisionOutcome!.Value.ToString(), denied ? app.DecisionWording?.ToString() : null)
                : null,
            denied ? app.OwnerReason : null,
            statement,
            approved ? app.ConditionsOfApproval : null,
            app.ClosedAt,
            CanWithdraw: app.Status == ArcApplicationStatus.Open,
            CanRevise: canRevise,
            revisions.Select(r => new ResidentArcRevisionDto(r.Id, r.Revision,
                ProjectStatus(r.Status, r.DecisionOutcome, r.Unanswered), r.ReceivedDate)).ToList());
    }

    /// <summary>Resident-visible events only, oldest first. No vote or vote-comment events.</summary>
    private static List<ResidentArcTimelineEventDto> Timeline(ArchitecturalApplication app)
    {
        var events = new List<ResidentArcTimelineEventDto> { new("Submitted", app.CreatedAt) };
        foreach (var info in app.InfoRequests)
        {
            events.Add(new ResidentArcTimelineEventDto("InfoRequested", info.RequestedAt));
            if (info.RespondedAt is { } responded)
                events.Add(new ResidentArcTimelineEventDto("InfoReplied", responded));
        }
        if (app.Status == ArcApplicationStatus.Closed && app.ClosedAt is { } closed)
        {
            var closeEvent = app.DecisionOutcome switch
            {
                ArcOutcome.Approved => "Approved",
                ArcOutcome.Withdrawn => "Withdrawn",
                _ => "Denied"
            };
            events.Add(new ResidentArcTimelineEventDto(closeEvent, app.WithdrawnAt ?? closed));
        }
        return events.OrderBy(e => e.At).ToList();
    }

    private static DateTimeOffset Latest(DateTimeOffset created, params DateTimeOffset?[] others) =>
        others.Where(o => o is not null).Select(o => o!.Value).Append(created).Max();
}

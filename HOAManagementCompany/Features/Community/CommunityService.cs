using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Community;

public class CommunityService(ApplicationDbContext db, IDocumentStorage storage)
{
    public async Task<AnnouncementListResponse> GetAnnouncementsAsync(string communityId, AnnouncementListRequest req, CancellationToken ct = default)
    {
        var query = db.Announcements.Where(a => a.CommunityId == communityId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Category) && Enum.TryParse<AnnouncementCategory>(req.Category, true, out var cat))
            query = query.Where(a => a.Category == cat);

        if (req.Pinned.HasValue)
            query = query.Where(a => a.Pinned == req.Pinned.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.PublishedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.Category.ToString(), a.PublishedAt, a.Pinned, a.LikeCount, a.CommentCount, a.AuthorName, a.AuthorRole, a.ImageUrl))
            .ToListAsync(ct);

        return new AnnouncementListResponse(items, total, req.Page, req.PageSize);
    }

    public async Task<AnnouncementDto> GetAnnouncementAsync(string communityId, Guid id, CancellationToken ct = default)
    {
        var a = await db.Announcements.FirstOrDefaultAsync(a => a.CommunityId == communityId && a.Id == id, ct)
            ?? throw new DomainException("NOT_FOUND", "Announcement not found.", 404);
        return new AnnouncementDto(a.Id, a.Title, a.Body, a.Category.ToString(), a.PublishedAt, a.Pinned, a.LikeCount, a.CommentCount, a.AuthorName, a.AuthorRole, a.ImageUrl);
    }

    public async Task<ViolationListResponse> GetViolationsAsync(Guid propertyId, ViolationListRequest req, CancellationToken ct = default)
    {
        var query = db.Violations.Where(v => v.PropertyId == propertyId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<ViolationStatus>(req.Status, true, out var status))
            query = query.Where(v => v.Status == status);

        if (!string.IsNullOrWhiteSpace(req.Category) && Enum.TryParse<ViolationCategory>(req.Category, true, out var cat))
            query = query.Where(v => v.Category == cat);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(v => v.IssuedDate)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(v => new ViolationDto(v.Id, v.Title, v.Description, v.Category.ToString(), v.Status.ToString(), v.IssuedDate, v.ResolvedDate, v.DueDate, v.FineAmount, v.ImageUrl))
            .ToListAsync(ct);

        return new ViolationListResponse(items, total, req.Page, req.PageSize);
    }

    public async Task<EventListResponse> GetEventsAsync(string communityId, EventListRequest req, CancellationToken ct = default)
    {
        var query = db.CalendarEvents.Where(e => e.CommunityId == communityId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.StartDate) && DateTimeOffset.TryParse(req.StartDate, out var start))
            query = query.Where(e => e.EventDate >= start);

        if (!string.IsNullOrWhiteSpace(req.EndDate) && DateTimeOffset.TryParse(req.EndDate, out var end))
            query = query.Where(e => e.EventDate <= end);

        if (!string.IsNullOrWhiteSpace(req.Category) && Enum.TryParse<EventCategory>(req.Category, true, out var cat))
            query = query.Where(e => e.Category == cat);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(e => e.EventDate)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(e => new EventDto(e.Id, e.Title, e.Description, e.EventDate, e.Location, e.Category.ToString(), e.RsvpEnabled, e.RsvpCount))
            .ToListAsync(ct);

        return new EventListResponse(items, total, req.Page, req.PageSize);
    }

    public async Task RsvpEventAsync(string communityId, Guid eventId, string userId, bool attending, CancellationToken ct = default)
    {
        var ev = await db.CalendarEvents.FirstOrDefaultAsync(e => e.CommunityId == communityId && e.Id == eventId, ct)
            ?? throw new DomainException("NOT_FOUND", "Event not found.", 404);

        var existing = await db.EventRsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId, ct);
        if (existing is null)
        {
            db.EventRsvps.Add(new Domain.Entities.EventRsvp { EventId = eventId, UserId = userId, Attending = attending });
            if (attending) ev.RsvpCount++;
        }
        else
        {
            var wasAttending = existing.Attending;
            existing.Attending = attending;
            existing.RsvpdAt = DateTimeOffset.UtcNow;
            if (!wasAttending && attending) ev.RsvpCount++;
            else if (wasAttending && !attending) ev.RsvpCount = Math.Max(0, ev.RsvpCount - 1);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<DocumentListResponse> GetDocumentsAsync(string communityId, DocumentListRequest req, CancellationToken ct = default)
    {
        var query = db.HoaDocuments.Where(d => d.CommunityId == communityId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Category) && Enum.TryParse<DocumentCategory>(req.Category, true, out var cat))
            query = query.Where(d => d.Category == cat);

        if (!string.IsNullOrWhiteSpace(req.Search))
            query = query.Where(d => d.Name.Contains(req.Search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.Pinned)
            .ThenByDescending(d => d.EffectiveDate)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(d => new DocumentDto(d.Id, d.Name, d.Category.ToString(), d.EffectiveDate, d.FileSizeLabel, d.Pinned))
            .ToListAsync(ct);

        return new DocumentListResponse(items, total, req.Page, req.PageSize);
    }

    public async Task<CommunityDirectoryResponse> GetCommunityDirectoryAsync(string communityId, Guid currentPropertyId, CancellationToken ct = default)
    {
        var totalHouseholds = await db.Properties.CountAsync(p => p.CommunityId == communityId, ct);

        // Find all properties in the community (excluding the current user's property)
        // and whose owners have at least one field marked as Shared
        var propertiesWithShared = await db.Properties
            .Where(p => p.CommunityId == communityId && p.Id != currentPropertyId)
            .Join(db.Owners, p => p.Id, o => o.PropertyId, (p, o) => new { Property = p, Owner = o })
            .ToListAsync(ct);

        var neighbors = new List<NeighborDto>();

        foreach (var row in propertiesWithShared)
        {
            var sharedFields = await db.DirectoryFields
                .Where(f => f.PropertyId == row.Property.Id && f.Shared)
                .ToListAsync(ct);

            if (sharedFields.Count == 0) continue;

            var sharedKeys = sharedFields.Select(f => f.FieldKey).ToHashSet();

            neighbors.Add(new NeighborDto(
                $"{row.Property.Address}, {row.Property.City}",
                sharedKeys.Contains("name") ? $"{row.Owner.FirstName} {row.Owner.LastName}" : null,
                sharedKeys.Contains("email") ? row.Owner.Email : null,
                sharedKeys.Contains("phone") ? row.Owner.Phone : null
            ));
        }

        return new CommunityDirectoryResponse(neighbors, neighbors.Count, totalHouseholds);
    }

    public async Task<DocumentDownloadResponse> GetDocumentDownloadUrlAsync(string communityId, Guid id, CancellationToken ct = default)
    {
        var doc = await db.HoaDocuments.FirstOrDefaultAsync(d => d.CommunityId == communityId && d.Id == id, ct)
            ?? throw new DomainException("NOT_FOUND", "Document not found.", 404);

        var url = await storage.GetPreSignedUrlAsync(doc.StorageKey, ct);
        return new DocumentDownloadResponse(url, DateTimeOffset.UtcNow.AddMinutes(5));
    }
}

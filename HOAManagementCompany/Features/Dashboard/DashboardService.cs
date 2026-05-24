using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Dashboard.Models;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Dashboard;

public class DashboardService(ApplicationDbContext db)
{
    public async Task<DashboardResponse> GetDashboardAsync(Guid propertyId, string communityId, CancellationToken ct = default)
    {
        var property = await db.Properties.FindAsync([propertyId], ct)
            ?? throw new Features.Auth.DomainException("NOT_FOUND", "Property not found.", 404);

        var latestEntry = await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId)
            .OrderByDescending(e => e.EntryDate)
            .FirstOrDefaultAsync(ct);

        var currentBalance = latestEntry?.RunningBalance ?? 0m;
        var balanceDueDate = CalculateDueDate(property.AssessmentDueDay);

        var openViolations = await db.Violations
            .CountAsync(v => v.PropertyId == propertyId && v.Status == ViolationStatus.Open, ct);

        var documentCount = await db.HoaDocuments.CountAsync(d => d.CommunityId == communityId, ct);
        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var newDocuments = await db.HoaDocuments
            .CountAsync(d => d.CommunityId == communityId && d.CreatedAt >= monthStart.ToDateTime(TimeOnly.MinValue), ct);

        var pinnedAnnouncement = await db.Announcements
            .Where(a => a.CommunityId == communityId && a.Pinned)
            .OrderByDescending(a => a.PublishedAt)
            .Select(a => new AnnouncementSummary(a.Id, a.Title, a.Body, a.Category.ToString(), a.PublishedAt, a.AuthorName))
            .FirstOrDefaultAsync(ct);

        var weekStart = DateTimeOffset.UtcNow.Date;
        var weekEnd = weekStart.AddDays(7);
        var thisWeekEvents = await db.CalendarEvents
            .Where(e => e.CommunityId == communityId && e.EventDate >= weekStart && e.EventDate < weekEnd)
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummary(e.Id, e.Title, e.EventDate, e.Location, e.Category.ToString(), e.RsvpEnabled))
            .ToListAsync(ct);

        var nextEvent = await db.CalendarEvents
            .Where(e => e.CommunityId == communityId && e.EventDate >= weekEnd)
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummary(e.Id, e.Title, e.EventDate, e.Location, e.Category.ToString(), e.RsvpEnabled))
            .FirstOrDefaultAsync(ct);

        var recentActivity = await db.LedgerEntries
            .Where(e => e.PropertyId == propertyId)
            .OrderByDescending(e => e.EntryDate)
            .Take(5)
            .Select(e => new LedgerSummary(e.Id, e.EntryDate, e.Description, e.ChargeAmount, e.PaymentAmount, e.RunningBalance, e.EntryType.ToString()))
            .ToListAsync(ct);

        var communityExpenses = await db.CommunityExpenses
            .Where(e => e.CommunityId == communityId && e.FiscalYear == DateTime.Today.Year)
            .Select(e => new ExpenseSummary(e.Id, e.Label, e.Color, e.Amount))
            .ToListAsync(ct);

        return new DashboardResponse(
            currentBalance,
            balanceDueDate,
            openViolations,
            documentCount,
            newDocuments,
            pinnedAnnouncement,
            thisWeekEvents,
            nextEvent,
            recentActivity,
            communityExpenses);
    }

    private static string CalculateDueDate(int assessmentDueDay)
    {
        var today = DateTime.Today;
        var thisMonth = new DateTime(today.Year, today.Month, Math.Min(assessmentDueDay, DateTime.DaysInMonth(today.Year, today.Month)));
        var dueDate = thisMonth >= today ? thisMonth : thisMonth.AddMonths(1);
        return dueDate.ToString("yyyy-MM-dd");
    }
}

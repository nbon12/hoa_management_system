using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;

namespace HOAManagementCompany.Seed;

public class CommunitySeeder(ApplicationDbContext db, SeedResult result, ILogger logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var communityId = result.CommunityId;
        var now = DateTimeOffset.UtcNow;

        db.Announcements.AddRange(
            new Announcement { CommunityId = communityId, Title = "Board Meeting – June 2026", Body = "The next board meeting will be held on June 10th at 7pm in the clubhouse.", PublishedAt = now.AddDays(-5), Category = AnnouncementCategory.Board, Pinned = true, AuthorName = "HOA Board", AuthorRole = "Board President" },
            new Announcement { CommunityId = communityId, Title = "Pool Maintenance Notice", Body = "The pool will be closed for maintenance June 1–3.", PublishedAt = now.AddDays(-10), Category = AnnouncementCategory.Maintenance, AuthorName = "HOA Management" },
            new Announcement { CommunityId = communityId, Title = "Summer BBQ Event", Body = "Join us for the annual community BBQ on July 4th!", PublishedAt = now.AddDays(-15), Category = AnnouncementCategory.Events, AuthorName = "HOA Events Committee" },
            new Announcement { CommunityId = communityId, Title = "Emergency Water Shutoff", Body = "Water will be shut off on May 28th from 9am–1pm for pipe repairs.", PublishedAt = now.AddDays(-1), Category = AnnouncementCategory.Emergencies, AuthorName = "Facilities Manager" },
            new Announcement { CommunityId = communityId, Title = "2026 Budget Approved", Body = "The 2026 operating budget has been approved by the board.", PublishedAt = now.AddDays(-30), Category = AnnouncementCategory.Board, AuthorName = "HOA Treasurer" });

        var poll = new Poll
        {
            CommunityId = communityId,
            Question = "Which community improvement should we prioritize for 2026?",
            ClosingLabel = "Closes June 30, 2026",
            IsActive = true,
            TotalVotes = 42,
            Options =
            [
                new PollOption { OptionIndex = 0, OptionText = "Upgrade the playground equipment", VoteCount = 18, Percentage = 42.86m },
                new PollOption { OptionIndex = 1, OptionText = "Resurface the tennis courts", VoteCount = 14, Percentage = 33.33m },
                new PollOption { OptionIndex = 2, OptionText = "Add covered bike parking", VoteCount = 10, Percentage = 23.81m }
            ]
        };
        db.Polls.Add(poll);

        db.Violations.AddRange(
            new Violation { PropertyId = result.PrimaryPropertyId, CommunityId = communityId, Title = "Overgrown hedges", Category = ViolationCategory.Landscape, Status = ViolationStatus.Open, IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-20)), DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)) },
            new Violation { PropertyId = result.PrimaryPropertyId, CommunityId = communityId, Title = "Unapproved fence installation", Category = ViolationCategory.Architectural, Status = ViolationStatus.Open, IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-45)), FineAmount = 150m },
            new Violation { PropertyId = result.PrimaryPropertyId, CommunityId = communityId, Title = "Parking violation – guest spot", Category = ViolationCategory.Parking, Status = ViolationStatus.Closed, IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-80)) },
            new Violation { PropertyId = result.PrimaryPropertyId, CommunityId = communityId, Title = "Noise complaint after 10pm", Category = ViolationCategory.Noise, Status = ViolationStatus.Closed, IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-60)), ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-55)) });

        db.CalendarEvents.AddRange(
            new CalendarEvent { CommunityId = communityId, Title = "Board Meeting", EventDate = now.AddDays(3), Location = "Clubhouse Room A", Category = EventCategory.Board, RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "Pool Opening Day", EventDate = now.AddDays(7), Location = "Community Pool", Category = EventCategory.Amenity, RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Summer BBQ", EventDate = now.AddDays(40), Location = "Community Park", Category = EventCategory.Social, RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "Landscaping Day", EventDate = now.AddDays(-7), Location = "Common Areas", Category = EventCategory.Maintenance, RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Tennis Tournament", EventDate = now.AddDays(14), Location = "Tennis Courts", Category = EventCategory.Amenity, RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "HOA Annual Meeting", EventDate = now.AddDays(60), Location = "Clubhouse Ballroom", Category = EventCategory.Board, RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "Movie Night", EventDate = now.AddDays(-14), Location = "Amphitheater", Category = EventCategory.Social, RsvpEnabled = false });

        db.CommunityExpenses.AddRange(
            new CommunityExpense { CommunityId = communityId, Label = "Landscaping", Color = "#4CAF50", Amount = 28500m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Pool Maintenance", Color = "#2196F3", Amount = 14200m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Security", Color = "#9C27B0", Amount = 18000m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Utilities", Color = "#FF9800", Amount = 9800m, FiscalYear = 2026 });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("CommunitySeeder complete.");
    }
}

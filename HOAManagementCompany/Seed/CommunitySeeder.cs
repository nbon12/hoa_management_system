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

        // ── Announcements ─────────────────────────────────────────────────
        db.Announcements.AddRange(
            new Announcement
            {
                CommunityId = communityId,
                Title = "Board Meeting – June 2026",
                Body = "The next regular board meeting will be held on Tuesday, June 10th at 7:00 PM in the Clubhouse Room A. All residents are welcome to attend. Agenda items include the 2027 budget preview, landscaping contract renewal, and gate access system upgrade.",
                PublishedAt = now.AddDays(-5), Category = AnnouncementCategory.Board,
                Pinned = true, LikeCount = 12, CommentCount = 4,
                AuthorName = "David Chen", AuthorRole = "Board President"
            },
            new Announcement
            {
                CommunityId = communityId,
                Title = "Pool Closed for Maintenance – June 1–3",
                Body = "The community pool will be temporarily closed June 1st through 3rd for annual filter replacement, deck resurfacing, and safety inspection. Normal hours (6 AM – 10 PM) resume June 4th.",
                PublishedAt = now.AddDays(-10), Category = AnnouncementCategory.Maintenance,
                LikeCount = 3, CommentCount = 1,
                AuthorName = "Facilities Team", AuthorRole = "Property Management"
            },
            new Announcement
            {
                CommunityId = communityId,
                Title = "4th of July Community BBQ 🎉",
                Body = "Join us for the annual Sakura Heights Independence Day BBQ! Friday July 4th from 4–9 PM at the Community Park. Live music, grills open, bring a side dish to share. RSVP by June 28th via the calendar.",
                PublishedAt = now.AddDays(-15), Category = AnnouncementCategory.Events,
                LikeCount = 28, CommentCount = 9,
                AuthorName = "Sarah Nakamura", AuthorRole = "Events Committee Chair"
            },
            new Announcement
            {
                CommunityId = communityId,
                Title = "⚠ Emergency Water Shutoff – May 28th",
                Body = "Water service will be interrupted on Wednesday, May 28th from 9:00 AM to 1:00 PM for emergency pipe repairs on Sakura Drive. Affected units: 1–48. Please store water in advance. We apologize for the inconvenience.",
                PublishedAt = now.AddDays(-1), Category = AnnouncementCategory.Emergencies,
                LikeCount = 0, CommentCount = 2,
                AuthorName = "Facilities Manager", AuthorRole = "Property Management"
            },
            new Announcement
            {
                CommunityId = communityId,
                Title = "2026 Operating Budget Approved",
                Body = "The board voted unanimously to approve the 2026 operating budget of $892,000. Key increases include +8% for landscaping (new contract), +$12,000 for parking lot resurfacing, and the establishment of a $50,000 reserve fund for the gate access upgrade project.",
                PublishedAt = now.AddDays(-30), Category = AnnouncementCategory.Board,
                LikeCount = 7, CommentCount = 3,
                AuthorName = "Maria Santos", AuthorRole = "HOA Treasurer"
            },
            new Announcement
            {
                CommunityId = communityId,
                Title = "New Dog Waste Stations Installed",
                Body = "Six new dog waste stations with dispensers have been installed along the walking trail and near the east parking lot. Please use them and dispose of bags in the designated bins. Thank you for keeping our community clean!",
                PublishedAt = now.AddDays(-45), Category = AnnouncementCategory.Maintenance,
                LikeCount = 19, CommentCount = 5,
                AuthorName = "Facilities Team", AuthorRole = "Property Management"
            },
            new Announcement
            {
                CommunityId = communityId,
                Title = "Architectural Review Committee – Summer Deadline",
                Body = "Planning exterior changes? All Architectural Review applications for summer projects (paint, fencing, landscaping alterations, additions) must be submitted by June 15th. Download the ARC form from the Documents library. Allow 14 business days for review.",
                PublishedAt = now.AddDays(-20), Category = AnnouncementCategory.Board,
                LikeCount = 4, CommentCount = 0,
                AuthorName = "HOA Board", AuthorRole = "Architectural Review Committee"
            });

        // ── Poll ──────────────────────────────────────────────────────────
        var poll = new Poll
        {
            CommunityId = communityId,
            Question = "Which community improvement should we prioritize for Q3 2026?",
            ClosingLabel = "Closes June 30, 2026",
            IsActive = true,
            TotalVotes = 87,
            Options =
            [
                new PollOption { OptionIndex = 0, OptionText = "Upgrade playground equipment",    VoteCount = 35, Percentage = 40.23m },
                new PollOption { OptionIndex = 1, OptionText = "Resurface tennis courts",         VoteCount = 27, Percentage = 31.03m },
                new PollOption { OptionIndex = 2, OptionText = "Add covered bike parking",        VoteCount = 15, Percentage = 17.24m },
                new PollOption { OptionIndex = 3, OptionText = "Install EV charging stations",   VoteCount = 10, Percentage = 11.49m }
            ]
        };
        db.Polls.Add(poll);

        // ── Violations ────────────────────────────────────────────────────
        db.Violations.AddRange(
            // Open
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Overgrown hedges obstructing sidewalk",
                Description = "Front hedges have grown beyond the 4-foot maximum height per CC&R Section 7.3 and are encroaching on the public sidewalk.",
                Category = ViolationCategory.Landscape, Status = ViolationStatus.Open,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-22)),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(8))
            },
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Unapproved fence installation – rear yard",
                Description = "A 6-foot wooden privacy fence was erected in the rear yard without prior ARC approval. ARC application must be submitted retroactively within 30 days.",
                Category = ViolationCategory.Architectural, Status = ViolationStatus.Open,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-47)),
                FineAmount = 150m,
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(15))
            },
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Holiday decorations still up in February",
                Description = "Per Rules Section 12.1, holiday exterior decorations must be removed by January 31st.",
                Category = ViolationCategory.Architectural, Status = ViolationStatus.Open,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(20))
            },
            // Closed
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Vehicle parked in visitor spot for 14+ days",
                Description = "A grey Toyota Camry (CA 7XYZ234) occupied visitor spot #12 for an extended period. Resolved after resident moved vehicle to assigned spot.",
                Category = ViolationCategory.Parking, Status = ViolationStatus.Closed,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-92)),
                ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-79))
            },
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Noise complaint – after-hours music (10 PM+)",
                Description = "Three separate complaints received from neighbors regarding amplified music after 10 PM on consecutive Friday nights. Resident was notified and complied.",
                Category = ViolationCategory.Noise, Status = ViolationStatus.Closed,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-62)),
                ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-55))
            },
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Trash bins left at curb for 3+ days",
                Description = "Trash and recycling bins left visible from street for 3 consecutive days after collection. Per Rules 8.2, bins must be stored within 24 hours of pickup.",
                Category = ViolationCategory.Maintenance, Status = ViolationStatus.Closed,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-120)),
                ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-115))
            },
            new Violation
            {
                PropertyId = result.PrimaryPropertyId, CommunityId = communityId,
                Title = "Unauthorized garage conversion",
                Description = "Garage door replaced with sliding glass door indicating a potential unpermitted conversion. Inspection confirmed it was a permitted home office addition; violation closed.",
                Category = ViolationCategory.Architectural, Status = ViolationStatus.Closed,
                IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-180)),
                ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-165))
            });

        // ── Calendar Events ───────────────────────────────────────────────
        db.CalendarEvents.AddRange(
            new CalendarEvent { CommunityId = communityId, Title = "Board Meeting",          EventDate = now.AddDays(3),   Location = "Clubhouse Room A",   Category = EventCategory.Board,       RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "Pool Opens for Season",  EventDate = now.AddDays(7),   Location = "Community Pool",     Category = EventCategory.Amenity,     RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "4th of July BBQ",        EventDate = now.AddDays(40),  Location = "Community Park",     Category = EventCategory.Social,      RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "Landscaping Day",        EventDate = now.AddDays(-7),  Location = "Common Areas",       Category = EventCategory.Maintenance, RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Tennis Tournament",      EventDate = now.AddDays(14),  Location = "Tennis Courts",      Category = EventCategory.Amenity,     RsvpEnabled = true,  RsvpCount = 12 },
            new CalendarEvent { CommunityId = communityId, Title = "HOA Annual Meeting",     EventDate = now.AddDays(60),  Location = "Clubhouse Ballroom", Category = EventCategory.Board,       RsvpEnabled = true },
            new CalendarEvent { CommunityId = communityId, Title = "Movie Night",            EventDate = now.AddDays(-14), Location = "Amphitheater",       Category = EventCategory.Social,      RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Pool Aerobics – Weekly", EventDate = now.AddDays(9),   Location = "Community Pool",     Category = EventCategory.Amenity,     RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Architectural Review Committee", EventDate = now.AddDays(21), Location = "Clubhouse Room B", Category = EventCategory.Board, RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Garage Sale Day",        EventDate = now.AddDays(28),  Location = "Community-wide",     Category = EventCategory.Social,      RsvpEnabled = true,  RsvpCount = 31 },
            new CalendarEvent { CommunityId = communityId, Title = "Tennis Courts Resurface",EventDate = now.AddDays(45),  Location = "Tennis Courts",      Category = EventCategory.Maintenance, RsvpEnabled = false },
            new CalendarEvent { CommunityId = communityId, Title = "Halloween Parade",       EventDate = now.AddDays(155), Location = "Sakura Drive",       Category = EventCategory.Social,      RsvpEnabled = true });

        // ── Community Expenses ────────────────────────────────────────────
        db.CommunityExpenses.AddRange(
            new CommunityExpense { CommunityId = communityId, Label = "Landscaping",       Color = "#4CAF50", Amount = 28500m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Pool Maintenance",  Color = "#2196F3", Amount = 14200m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Security",          Color = "#9C27B0", Amount = 18000m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Utilities",         Color = "#FF9800", Amount = 9800m,  FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Admin & Legal",     Color = "#F44336", Amount = 12400m, FiscalYear = 2026 },
            new CommunityExpense { CommunityId = communityId, Label = "Insurance",         Color = "#607D8B", Amount = 21500m, FiscalYear = 2026 });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("CommunitySeeder complete.");
    }
}

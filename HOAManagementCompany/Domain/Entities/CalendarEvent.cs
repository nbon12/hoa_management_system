using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; }
    public string CommunityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset EventDate { get; set; }
    public string? Location { get; set; }
    public EventCategory Category { get; set; }
    public bool RsvpEnabled { get; set; }
    public int RsvpCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<EventRsvp> Rsvps { get; set; } = [];
}

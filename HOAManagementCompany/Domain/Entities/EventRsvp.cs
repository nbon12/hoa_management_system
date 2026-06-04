namespace HOAManagementCompany.Domain.Entities;

public class EventRsvp
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool Attending { get; set; }
    public DateTimeOffset RsvpdAt { get; set; } = DateTimeOffset.UtcNow;

    public CalendarEvent Event { get; set; } = null!;
}

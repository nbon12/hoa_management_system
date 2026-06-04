namespace HOAManagementCompany.Domain.Entities;

public class AddressHistory
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
}

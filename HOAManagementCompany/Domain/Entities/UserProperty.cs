namespace HOAManagementCompany.Domain.Entities;

public class UserProperty
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid PropertyId { get; set; }
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public Property Property { get; set; } = null!;
}

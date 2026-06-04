using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class Violation
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string CommunityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ViolationCategory Category { get; set; }
    public ViolationStatus Status { get; set; }
    public DateOnly IssuedDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? FineAmount { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;
}

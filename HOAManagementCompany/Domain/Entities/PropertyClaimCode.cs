namespace HOAManagementCompany.Domain.Entities;

// 016-A FR-A1/A1a: one-time, single-use, 90-day code that authorizes binding a user to a property.
// Delivered out-of-band to the owner's contact on file; only the hash is stored.
public class PropertyClaimCode
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public string DeliveredToContact { get; set; } = string.Empty; // masked destination (audit)
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public string? RedeemedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Property Property { get; set; } = null!;

    public bool IsActive => RedeemedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}

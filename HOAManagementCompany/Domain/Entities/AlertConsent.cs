namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Proof of SMS/email consent and opt-out, sufficient for TCPA (FR-031). Append-only history.
/// </summary>
public class AlertConsent
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }

    /// <summary><c>sms</c> or <c>email</c>.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary><c>opt_in</c> or <c>opt_out</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public string? ConsentText { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? SourceIp { get; set; }

    public Owner Owner { get; set; } = null!;
}

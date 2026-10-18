namespace HOAManagementCompany.Domain.Entities;

// 030 R6/R12: one row per invitation email attempt. Feeds the per-recipient cap; the activation
// fan-out is recorded but bypasses the cap (FR-041c).
public class InvitationDelivery
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }
    public string NormalizedEmail { get; set; } = string.Empty;
    public DateTimeOffset AttemptedAt { get; set; }
    public bool Succeeded { get; set; }
    public bool BypassesRecipientCap { get; set; }

    public Invitation Invitation { get; set; } = null!;
}

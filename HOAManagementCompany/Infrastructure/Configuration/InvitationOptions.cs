namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Invitation limits (030 FR-022, FR-041c; research R12). Bound from <c>"Invitations"</c> and
/// validated at startup by <see cref="InvitationOptionsValidator"/>.
/// </summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    /// <summary>How long a link works after it is emailed. Fixed at 72 by the spec; validated so it can't drift.</summary>
    public int ExpiryHours { get; set; } = 72;

    /// <summary>First sends and resends to one email address in a rolling 24 hours (the activation fan-out is exempt).</summary>
    public int MaxEmailsPerRecipientPer24h { get; set; } = 5;

    /// <summary>Minimum time between two sends of the same invitation.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;
}

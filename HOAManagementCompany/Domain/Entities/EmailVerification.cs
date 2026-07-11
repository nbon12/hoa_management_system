namespace HOAManagementCompany.Domain.Entities;

// 016-A FR-A3: email-verification gate. Proves control of an email before any registration/claim
// state is revealed, and is reused for the verified email-change flow (019-C FR-C6).
public class EmailVerification
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;   // normalized, lowercased
    public string Purpose { get; set; } = string.Empty; // "registration" | "email_change"
    public string? UserId { get; set; }                 // null pre-registration; set for email-change

    public string CodeHash { get; set; } = string.Empty; // SHA-256 of the one-time code (never raw)
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptCount { get; set; }

    // Post-confirmation proof: a short-lived token the caller presents to /auth/register.
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ProofHash { get; set; }
    public DateTimeOffset? ProofExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }      // proof spent (registration completed)

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool CodeIsActive => ConfirmedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
    public bool ProofIsActive => ConfirmedAt is not null && ConsumedAt is null
        && ProofExpiresAt is not null && DateTimeOffset.UtcNow < ProofExpiresAt;
}

using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// Invitation: an emailed offer of a community membership, residency of one property, or the
// Company Administrator role (030 FR-022). Scheduled (residency in an Onboarding community) →
// Pending (emailed; usable for 72 hours) → Accepted or Revoked, both final. Expired is derived:
// Pending with now >= ExpiresAt. Only a SHA-256 hash of the link token is stored, and a resend
// replaces it, so old links stop working. Records are never deleted; the id stands in for the
// invited email in logs (FR-040).
// <!-- REPOWISE:END -->

public class Invitation
{
    public Guid Id { get; set; }
    public InvitationKind Kind { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? CommunityId { get; set; }
    public Guid? PropertyId { get; set; }
    public CommunityRole? Role { get; set; }
    public DateOnly? TermEndDate { get; set; }

    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the current link token. Null while Scheduled; replaced on resend.</summary>
    public string? TokenHash { get; set; }
    public InvitationState State { get; set; }

    /// <summary>The current sender (updated on resend). Null only for a setup-step Company Administrator invitation.</summary>
    public string? SenderUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int ResendCount { get; set; }
    public string? LastSendError { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }
    public InvitationRevokeReason? RevokeReason { get; set; }

    public ManagementCompany Company { get; set; } = null!;
    public Community? Community { get; set; }
    public Property? Property { get; set; }
    public ApplicationUser? Sender { get; set; }
    public ICollection<InvitationDelivery> Deliveries { get; set; } = [];

    /// <summary>The column length of <see cref="Email"/> and <see cref="NormalizedEmail"/>.</summary>
    public const int MaxEmailLength = 256;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public bool IsExpired(DateTimeOffset now) => State == InvitationState.Pending && ExpiresAt is { } e && now >= e;

    /// <summary>The state as shown to people: Pending past its expiry reads as Expired.</summary>
    public string EffectiveState(DateTimeOffset now) => IsExpired(now) ? "Expired" : State.ToString();

    /// <summary>Scheduled or Pending (including expired-but-not-revoked).</summary>
    public bool IsOpen => State is InvitationState.Scheduled or InvitationState.Pending;
}

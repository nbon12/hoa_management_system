namespace HOAManagementCompany.Domain.Enums;

// 030 FR-022 / data-model.md. Expired is not stored: it is Pending with now >= ExpiresAt.
public enum InvitationState
{
    Scheduled,
    Pending,
    Accepted,
    Revoked
}

namespace HOAManagementCompany.Domain.Enums;

// 030 data-model.md: why an invitation was revoked.
public enum InvitationRevokeReason
{
    Manual,
    CommunityArchived,
    SenderLostAuthority,
    // A new invitation for the same target replaced this expired one (research R6).
    Superseded
}

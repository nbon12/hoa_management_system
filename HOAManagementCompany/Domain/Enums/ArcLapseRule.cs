namespace HOAManagementCompany.Domain.Enums;

// What happens when the review period ends without a decision (027 FR-027).
public enum ArcLapseRule
{
    FlagOverdueOnly,
    DeemedApproved,
    DeemedDenied
}

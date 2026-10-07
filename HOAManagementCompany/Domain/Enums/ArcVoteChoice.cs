namespace HOAManagementCompany.Domain.Enums;

// A board member's vote (027 FR-015). RevisionsNeeded and Deny both count toward
// the denial side; RevisionsNeeded only changes the owner-facing wording (FR-023).
public enum ArcVoteChoice
{
    Approve,
    RevisionsNeeded,
    Deny
}

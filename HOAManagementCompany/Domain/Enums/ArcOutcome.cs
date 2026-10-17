namespace HOAManagementCompany.Domain.Enums;

// The legal record of a decision (027 FR-025). "Revisions requested" is a Denied
// outcome with RevisionsRequested wording, never a separate outcome. Withdrawn (029) is the
// resident closing an undecided application themselves: it is not a board decision, never
// produces an owner outcome email, and is hidden from the board's default views.
public enum ArcOutcome
{
    Approved,
    Denied,
    Withdrawn
}

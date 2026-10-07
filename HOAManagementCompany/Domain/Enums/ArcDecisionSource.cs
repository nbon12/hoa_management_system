namespace HOAManagementCompany.Domain.Enums;

// How a decision was reached: by board votes, or by the community's lapse rule
// when the review period ended ("by default (review period lapsed)", 027 FR-027).
public enum ArcDecisionSource
{
    Votes,
    Lapse
}

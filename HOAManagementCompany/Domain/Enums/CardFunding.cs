namespace HOAManagementCompany.Domain.Enums;

/// <summary>Card funding type from Stripe's <c>card.funding</c> (FR-004b). Drives surcharge eligibility.</summary>
public enum CardFunding
{
    Credit,
    Debit,
    Prepaid,
    Unknown
}

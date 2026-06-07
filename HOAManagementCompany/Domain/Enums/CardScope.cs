namespace HOAManagementCompany.Domain.Enums;

/// <summary>Which card funding types a fee applies to. <c>Percentage</c> fees require <see cref="CreditOnly"/>.</summary>
public enum CardScope
{
    AllCards,
    CreditOnly
}

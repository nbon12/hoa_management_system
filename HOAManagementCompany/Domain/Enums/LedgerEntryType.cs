namespace HOAManagementCompany.Domain.Enums;

public enum LedgerEntryType
{
    // Existing (string-persisted — do NOT rename).
    RegularAssessment,
    Payment,
    LateFee,
    FinanceCharge,

    // New for Stripe payments (FR-007e).
    Refund,
    Reversal,
    Chargeback,
    ReturnedPaymentFee,
    Credit,
    Adjustment
}

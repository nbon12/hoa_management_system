using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Tests.Factories;

/// <summary>
/// Valid payment-domain test data (015 US6, FR-017 — per-domain factories instead of coupling to
/// the global seeder's magic ids). Declares data only; no business rules (testing constitution
/// §2.3). Every identifier is unique per call, so tests stay parallel- and rerun-safe.
/// </summary>
public static class PaymentFactory
{
    public static PaymentTransaction Transaction(
        Guid propertyId,
        Guid ownerId,
        TransactionStatus status = TransactionStatus.Succeeded,
        PaymentMethod method = PaymentMethod.Card,
        decimal gross = 250m,
        decimal fee = 0m) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = propertyId,
        OwnerId = ownerId,
        StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
        StripeChargeId = $"ch_test_{Guid.NewGuid():N}",
        GrossAmount = gross,
        FeeAmount = fee,
        Total = gross + fee,
        Status = status,
        PaymentMethod = method,
    };

    public static HoaPaymentConfig NsfEnabledConfig(string communityId, decimal nsfFee = 25m) => new()
    {
        Id = Guid.NewGuid(),
        CommunityId = communityId,
        NsfFeeEnabled = true,
        NsfFeeAmount = nsfFee,
    };
}

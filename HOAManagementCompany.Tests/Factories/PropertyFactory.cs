using HOAManagementCompany.Domain.Entities;

namespace HOAManagementCompany.Tests.Factories;

public static class PropertyFactory
{
    public static Property Create(
        string communityId = "SAKURA",
        string accountNumber = "TEST001")
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            AccountNumber = accountNumber,
            CommunityId = communityId,
            CommunityName = "Sakura Heights HOA",
            Address = "123 Sakura Blvd",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A1",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };
    }
}

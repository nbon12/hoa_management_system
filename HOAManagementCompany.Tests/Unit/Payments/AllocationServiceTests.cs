using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Ledger;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Payments;

public class AllocationServiceTests
{
    private static OpenCharge Charge(LedgerEntryType cat, int daysAgo, decimal amount) =>
        new(Guid.NewGuid(), cat, DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo)), amount);

    [Fact]
    public void Allocate_PaysByCategoryPriority_ThenOldestFirst()
    {
        var lateFee = Charge(LedgerEntryType.LateFee, 5, 50m);
        var newAssessment = Charge(LedgerEntryType.RegularAssessment, 1, 250m);
        var oldAssessment = Charge(LedgerEntryType.RegularAssessment, 30, 250m);

        var result = AllocationService.Allocate(
            new[] { lateFee, newAssessment, oldAssessment }, 300m, AllocationService.DefaultOrder);

        // RegularAssessment outranks LateFee; within assessments, oldest first.
        Assert.Equal(2, result.Allocations.Count);
        Assert.Equal(oldAssessment.Id, result.Allocations[0].ChargeId);
        Assert.Equal(250m, result.Allocations[0].Applied);
        Assert.Equal(newAssessment.Id, result.Allocations[1].ChargeId);
        Assert.Equal(50m, result.Allocations[1].Applied);
        Assert.Equal(0m, result.Surplus);
    }

    [Fact]
    public void Allocate_Overpayment_ReturnsSurplus()
    {
        var charge = Charge(LedgerEntryType.RegularAssessment, 1, 100m);
        var result = AllocationService.Allocate(new[] { charge }, 250m, AllocationService.DefaultOrder);

        Assert.Single(result.Allocations);
        Assert.Equal(100m, result.Allocations[0].Applied);
        Assert.Equal(150m, result.Surplus);
    }

    [Fact]
    public void Allocate_PartialPayment_StopsWhenExhausted()
    {
        var a = Charge(LedgerEntryType.RegularAssessment, 2, 100m);
        var b = Charge(LedgerEntryType.RegularAssessment, 1, 100m);
        var result = AllocationService.Allocate(new[] { a, b }, 100m, AllocationService.DefaultOrder);

        Assert.Single(result.Allocations);
        Assert.Equal(a.Id, result.Allocations[0].ChargeId);
        Assert.Equal(0m, result.Surplus);
    }

    [Fact]
    public void Allocate_ZeroPayment_NoAllocations()
    {
        var charge = Charge(LedgerEntryType.RegularAssessment, 1, 100m);
        var result = AllocationService.Allocate(new[] { charge }, 0m, AllocationService.DefaultOrder);
        Assert.Empty(result.Allocations);
        Assert.Equal(0m, result.Surplus);
    }

    [Fact]
    public void Allocate_UnknownCategory_RanksLast()
    {
        var known = Charge(LedgerEntryType.RegularAssessment, 1, 50m);
        var unknown = Charge(LedgerEntryType.Credit, 30, 50m);    // not in DefaultOrder
        var result = AllocationService.Allocate(new[] { unknown, known }, 50m, AllocationService.DefaultOrder);

        Assert.Single(result.Allocations);
        Assert.Equal(known.Id, result.Allocations[0].ChargeId);
    }

    [Fact]
    public void ParseOrder_NullConfig_ReturnsDefault()
    {
        Assert.Equal(AllocationService.DefaultOrder, AllocationService.ParseOrder(null));
    }

    [Fact]
    public void ParseOrder_ValidJson_ParsesAndOrders()
    {
        var config = new HoaPaymentConfig { AllocationOrderJson = "[\"LateFee\",\"RegularAssessment\"]" };
        var order = AllocationService.ParseOrder(config);
        Assert.Equal(LedgerEntryType.LateFee, order[0]);
        Assert.Equal(LedgerEntryType.RegularAssessment, order[1]);
    }

    [Fact]
    public void ParseOrder_WhitespaceJson_ReturnsDefault()
    {
        var config = new HoaPaymentConfig { AllocationOrderJson = "   " };
        Assert.Equal(AllocationService.DefaultOrder, AllocationService.ParseOrder(config));
    }

    [Fact]
    public void ParseOrder_InvalidJson_ReturnsDefault()
    {
        var config = new HoaPaymentConfig { AllocationOrderJson = "not-json" };
        Assert.Equal(AllocationService.DefaultOrder, AllocationService.ParseOrder(config));
    }

    [Fact]
    public void ParseOrder_AllUnknownNames_ReturnsDefault()
    {
        var config = new HoaPaymentConfig { AllocationOrderJson = "[\"Nope\",\"AlsoNope\"]" };
        Assert.Equal(AllocationService.DefaultOrder, AllocationService.ParseOrder(config));
    }
}

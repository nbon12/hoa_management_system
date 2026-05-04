using HOAManagementCompany.Models;
using HOAManagementCompany.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests;

public class DashboardServiceTests : TestBase
{
    [Fact]
    public async Task GetSummaryAsync_WhenUserIdIsNull_ReturnsZeroViolationCount()
    {
        var dashboardService = ServiceProvider.GetRequiredService<DashboardService>();
        var result = await dashboardService.GetSummaryAsync(null);
        Assert.NotNull(result);
        Assert.Equal(0, result.OpenViolationCount);
        Assert.Null(result.CurrentBalance);
        Assert.Null(result.WorkOrdersCount);
        Assert.Null(result.ArchitectureRequestsCount);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenUserIdHasNoProperties_ReturnsZeroViolationCount()
    {
        var dashboardService = ServiceProvider.GetRequiredService<DashboardService>();
        var result = await dashboardService.GetSummaryAsync("non-existent-user-id");
        Assert.NotNull(result);
        Assert.Equal(0, result.OpenViolationCount);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenUserHasOpenViolations_ReturnsCorrectCount()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetSummaryAsync_WhenUserHasOpenViolations_ReturnsCorrectCount));
        try
        {
            var violationType = await CreateTestViolationTypeAsync(ns, "GRASS", "Covenant");
            var property = await CreateTestPropertyAsync(ns, "MyProp");
            DbContext.ChangeTracker.Clear();
            var v1 = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_V1",
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id,
                PropertyId = property.Id
            };
            var v2 = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_V2",
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id,
                PropertyId = property.Id
            };
            DbContext.Violations.AddRange(v1, v2);
            await DbContext.SaveChangesAsync();
            var dashboardService = ServiceProvider.GetRequiredService<DashboardService>();
            var result = await dashboardService.GetSummaryAsync(property.OwnerUserId);
            Assert.NotNull(result);
            Assert.Equal(2, result.OpenViolationCount);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_WhenUserHasClosedViolations_DoesNotCountThem()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetSummaryAsync_WhenUserHasClosedViolations_DoesNotCountThem));
        try
        {
            var violationType = await CreateTestViolationTypeAsync(ns, "GRASS", "Covenant");
            var property = await CreateTestPropertyAsync(ns, "MyProp");
            DbContext.ChangeTracker.Clear();
            var closedV = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_Closed",
                Status = ViolationStatus.Closed,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id,
                PropertyId = property.Id
            };
            var openV = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_Open",
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id,
                PropertyId = property.Id
            };
            DbContext.Violations.AddRange(closedV, openV);
            await DbContext.SaveChangesAsync();
            var dashboardService = ServiceProvider.GetRequiredService<DashboardService>();
            var result = await dashboardService.GetSummaryAsync(property.OwnerUserId);
            Assert.NotNull(result);
            Assert.Equal(1, result.OpenViolationCount);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }
}

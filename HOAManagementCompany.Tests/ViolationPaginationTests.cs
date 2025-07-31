using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HOAManagementCompany.Models;
using HOAManagementCompany.Services;
using Xunit;

namespace HOAManagementCompany.Tests;

public class ViolationPaginationTests : TestBase
{
    [Fact]
    public async Task GetViolationsPagedAsync_WithDefaultParameters_ShouldReturnFirstPage()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithDefaultParameters_ShouldReturnFirstPage));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create 25 violations to test pagination
            for (int i = 1; i <= 25; i++)
            {
                await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Violation {i}", ViolationStatus.Open);
            }

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.CurrentPage);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(3, result.TotalPages); // 25 items / 10 per page = 3 pages
            Assert.True(result.HasNext);
            Assert.False(result.HasPrevious);
            Assert.Equal(10, result.Count); // Should have 10 items on first page
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithSecondPage_ShouldReturnCorrectItems()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithSecondPage_ShouldReturnCorrectItems));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create 15 violations
            for (int i = 1; i <= 15; i++)
            {
                await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Violation {i}", ViolationStatus.Open);
            }

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters { PageNumber = 2, PageSize = 10 };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.CurrentPage);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(15, result.TotalCount);
            Assert.Equal(2, result.TotalPages); // 15 items / 10 per page = 2 pages
            Assert.False(result.HasNext);
            Assert.True(result.HasPrevious);
            Assert.Equal(5, result.Count); // Should have 5 items on second page
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithCustomPageSize_ShouldRespectPageSize()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithCustomPageSize_ShouldRespectPageSize));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create 20 violations
            for (int i = 1; i <= 20; i++)
            {
                await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Violation {i}", ViolationStatus.Open);
            }

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters { PageNumber = 1, PageSize = 5 };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.CurrentPage);
            Assert.Equal(5, result.PageSize);
            Assert.Equal(20, result.TotalCount);
            Assert.Equal(4, result.TotalPages); // 20 items / 5 per page = 4 pages
            Assert.True(result.HasNext);
            Assert.False(result.HasPrevious);
            Assert.Equal(5, result.Count); // Should have 5 items
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithSearchTerm_ShouldFilterResults()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithSearchTerm_ShouldFilterResults));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create violations with different descriptions
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Lawn violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Parking violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Another lawn issue", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Noise complaint", ViolationStatus.Open);

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters 
            { 
                PageNumber = 1, 
                PageSize = 10, 
                SearchTerm = "lawn" 
            };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalCount); // Should find 2 lawn-related violations
            Assert.Equal(1, result.TotalPages);
            Assert.Equal(2, result.Count);
            Assert.All(result, v => Assert.Contains("lawn", v.Description.ToLower()));
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithOrderBy_ShouldSortResults()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithOrderBy_ShouldSortResults));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create violations with different descriptions
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Zebra violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Alpha violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Beta violation", ViolationStatus.Open);

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters 
            { 
                PageNumber = 1, 
                PageSize = 10, 
                OrderBy = "Description",
                OrderDesc = false
            };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            
            // Should be sorted alphabetically ascending
            var descriptions = result.Select(v => v.Description).ToList();
            Assert.Contains("Alpha violation", descriptions[0]);
            Assert.Contains("Beta violation", descriptions[1]);
            Assert.Contains("Zebra violation", descriptions[2]);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithOrderByDescending_ShouldSortResultsDescending()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithOrderByDescending_ShouldSortResultsDescending));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create violations with different descriptions
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Zebra violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Alpha violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Beta violation", ViolationStatus.Open);

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters 
            { 
                PageNumber = 1, 
                PageSize = 10, 
                OrderBy = "Description",
                OrderDesc = true
            };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            
            // Should be sorted alphabetically descending
            var descriptions = result.Select(v => v.Description).ToList();
            Assert.Contains("Zebra violation", descriptions[0]);
            Assert.Contains("Beta violation", descriptions[1]);
            Assert.Contains("Alpha violation", descriptions[2]);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithInvalidPageNumber_ShouldDefaultToPageOne()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithInvalidPageNumber_ShouldDefaultToPageOne));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create 5 violations
            for (int i = 1; i <= 5; i++)
            {
                await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Violation {i}", ViolationStatus.Open);
            }

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters { PageNumber = 0, PageSize = 10 }; // Invalid page number

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.CurrentPage); // Should default to page 1
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(1, result.TotalPages);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithInvalidPageSize_ShouldRespectMaxPageSize()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithInvalidPageSize_ShouldRespectMaxPageSize));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create 200 violations
            for (int i = 1; i <= 200; i++)
            {
                await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Violation {i}", ViolationStatus.Open);
            }

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters { PageNumber = 1, PageSize = 150 }; // Exceeds max page size

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.PageSize); // Should be capped at 100
            Assert.Equal(200, result.TotalCount);
            Assert.Equal(2, result.TotalPages); // 200 items / 100 per page = 2 pages
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithEmptyDatabase_ShouldReturnEmptyPage()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithEmptyDatabase_ShouldReturnEmptyPage));
        try
        {
            // Arrange - no violations created
            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await service.GetViolationsPagedAsync(paginationParams, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalCount);
            Assert.Equal(0, result.TotalPages);
            Assert.Equal(0, result.Count);
            Assert.False(result.HasNext);
            Assert.False(result.HasPrevious);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithStatusFilter_ShouldFilterByStatus()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithStatusFilter_ShouldFilterByStatus));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_TYPE", "Test covenant");
            
            // Create violations with different statuses
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Open violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Closed violation", ViolationStatus.Closed);
            await CreateTestViolationAsync(ns, violationType.Id, $"{ns}_Another open violation", ViolationStatus.Open);

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters 
            { 
                PageNumber = 1, 
                PageSize = 10
            };

            // Act - filter by open status
            var result = await service.GetViolationsPagedAsync(paginationParams, status: ViolationStatus.Open, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalCount); // Should find 2 open violations
            Assert.Equal(1, result.TotalPages);
            Assert.Equal(2, result.Count);
            Assert.All(result, v => Assert.Equal(ViolationStatus.Open, v.Status));
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsPagedAsync_WithViolationTypeFilter_ShouldFilterByType()
    {
        var ns = GenerateUniqueTestNamespace(nameof(GetViolationsPagedAsync_WithViolationTypeFilter_ShouldFilterByType));
        try
        {
            // Arrange
            var violationType1 = await CreateTestViolationTypeAsync(ns, "TYPE_1", "First covenant");
            var violationType2 = await CreateTestViolationTypeAsync(ns, "TYPE_2", "Second covenant");
            
            // Create violations with different types
            await CreateTestViolationAsync(ns, violationType1.Id, $"{ns}_Type 1 violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType2.Id, $"{ns}_Type 2 violation", ViolationStatus.Open);
            await CreateTestViolationAsync(ns, violationType1.Id, $"{ns}_Another type 1 violation", ViolationStatus.Open);

            var service = new ViolationService(ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
            var paginationParams = new PaginationParameters 
            { 
                PageNumber = 1, 
                PageSize = 10
            };

            // Act - filter by violation type
            var result = await service.GetViolationsPagedAsync(paginationParams, violationTypeId: violationType1.Id, testNamespace: ns);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalCount); // Should find 2 violations of type 1
            Assert.Equal(1, result.TotalPages);
            Assert.Equal(2, result.Count);
            Assert.All(result, v => Assert.Equal(violationType1.Id, v.ViolationTypeId));
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }
} 
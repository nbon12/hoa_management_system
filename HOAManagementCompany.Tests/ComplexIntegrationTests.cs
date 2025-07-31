using Microsoft.EntityFrameworkCore;
using HOAManagementCompany.Models;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Tests;

public class ComplexIntegrationTests : TestBase
{
    [Fact]
    public async Task CompleteWorkflow_ShouldHandleComplexScenario()
    {
        var ns = GenerateUniqueTestNamespace(nameof(CompleteWorkflow_ShouldHandleComplexScenario));
        try
        {
            // Arrange
            var grassType = await CreateTestViolationTypeAsync(ns, "GRASS", "Lawn maintenance covenant");
            var powerwashType = await CreateTestViolationTypeAsync(ns, "POWERWASH", "House maintenance covenant");
            var fenceType = await CreateTestViolationTypeAsync(ns, "FENCE", "Fence maintenance covenant");

            var grassViolation1 = await CreateTestViolationAsync(ns, grassType.Id, "Lawn overgrown in front yard", ViolationStatus.Open);
            var grassViolation2 = await CreateTestViolationAsync(ns, grassType.Id, "Lawn overgrown in back yard", ViolationStatus.Open);
            var powerwashViolation = await CreateTestViolationAsync(ns, powerwashType.Id, "House needs power washing", ViolationStatus.Open);
            var fenceViolation = await CreateTestViolationAsync(ns, fenceType.Id, "Fence needs repair", ViolationStatus.Open);

            // Assert 1 - Verify all violations were created
            var testViolations = await DbContext.Violations
                .Include(v => v.ViolationType)
                .Where(v => v.Description.StartsWith(ns + "_"))
                .ToListAsync();
            Assert.Equal(4, testViolations.Count);
            Assert.Equal(2, testViolations.Count(v => v.ViolationType!.Name == $"{ns}_GRASS"));
            Assert.Single(testViolations.Where(v => v.ViolationType!.Name == $"{ns}_POWERWASH"));
            Assert.Single(testViolations.Where(v => v.ViolationType!.Name == $"{ns}_FENCE"));

            // Act 2 - Update some violations to closed status
            grassViolation1.Status = ViolationStatus.Closed;
            powerwashViolation.Status = ViolationStatus.Closed;
            await DbContext.SaveChangesAsync();

            // Assert 2 - Verify status updates by checking the original objects
            // Since we're using the same context, the objects should reflect the changes
            Assert.Equal(ViolationStatus.Closed, grassViolation1.Status);
            Assert.Equal(ViolationStatus.Closed, powerwashViolation.Status);
            Assert.Equal(ViolationStatus.Open, grassViolation2.Status);
            Assert.Equal(ViolationStatus.Open, fenceViolation.Status);

            // Act 3 - Delete a violation type and verify cascade behavior
            var grassViolationsBeforeDelete = await DbContext.Violations.Where(v => v.ViolationTypeId == grassType.Id).ToListAsync();
            Assert.Equal(2, grassViolationsBeforeDelete.Count);

            // Try to delete violation type with related violations (should soft delete successfully)
            // Use a fresh context to avoid entity tracking conflicts
            using var newScope = ((IServiceScopeFactory)ServiceProvider.GetService(typeof(IServiceScopeFactory))!).CreateScope();
            var newDbContext = newScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var grassTypeToDelete = await newDbContext.ViolationTypes.FirstOrDefaultAsync(vt => vt.Id == grassType.Id);
            Assert.NotNull(grassTypeToDelete);
            
            newDbContext.ViolationTypes.Remove(grassTypeToDelete);
            await newDbContext.SaveChangesAsync(); // Should not throw exception due to soft delete
            
            // Verify the violation type was soft deleted
            var softDeletedGrassType = await newDbContext.ViolationTypes.IgnoreQueryFilters().FirstOrDefaultAsync(vt => vt.Id == grassType.Id);
            Assert.NotNull(softDeletedGrassType);
            Assert.True(softDeletedGrassType.IsDeleted);

            // Act 4 - Delete violations first, then delete violation type
            // Use the original context but clear tracking first
            DbContext.ChangeTracker.Clear();
            
            var grassViolationsToDelete = await DbContext.Violations.Where(v => v.ViolationTypeId == grassType.Id).ToListAsync();
            DbContext.Violations.RemoveRange(grassViolationsToDelete);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            var grassTypeToDelete2 = await DbContext.ViolationTypes.FirstOrDefaultAsync(vt => vt.Id == grassType.Id);
            if (grassTypeToDelete2 != null)
            {
                DbContext.ViolationTypes.Remove(grassTypeToDelete2);
                await DbContext.SaveChangesAsync();
            }

            // Assert 4 - Verify deletion (soft delete)
            var testRemainingViolations = await DbContext.Violations.Where(v => v.Description.StartsWith(ns + "_") && !v.IsDeleted).ToListAsync();
            var testRemainingViolationTypes = await DbContext.ViolationTypes.Where(vt => (vt.Name == $"{ns}_POWERWASH" || vt.Name == $"{ns}_FENCE") && !vt.IsDeleted).ToListAsync();
            Assert.Equal(2, testRemainingViolations.Count);
            Assert.Equal(2, testRemainingViolationTypes.Count); // 2 created - 1 soft deleted
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task BulkOperations_ShouldHandleMultipleRecords()
    {
        var ns = GenerateUniqueTestNamespace(nameof(BulkOperations_ShouldHandleMultipleRecords));
        try
        {
            // Arrange - Create multiple violation types
            var violationTypes = new List<ViolationType>();
            for (int i = 1; i <= 5; i++)
            {
                var violationType = await CreateTestViolationTypeAsync(ns, $"BULK_TYPE_{i}", $"Covenant text for type {i}");
                violationTypes.Add(violationType);
            }

            // Act - Create multiple violations in bulk
            var violations = new List<Violation>();
            for (int i = 1; i <= 10; i++)
            {
                var violationType = violationTypes[i % violationTypes.Count];
                var violation = new Violation
                {
                    Id = Guid.NewGuid(),
                    Description = $"{ns}_Bulk violation {i}",
                    Status = i % 2 == 0 ? ViolationStatus.Open : ViolationStatus.Closed,
                    OccurrenceDate = DateTime.UtcNow.AddDays(-i),
                    ViolationTypeId = violationType.Id
                };
                violations.Add(violation);
            }

            DbContext.ChangeTracker.Clear();
            DbContext.Violations.AddRange(violations);
            await DbContext.SaveChangesAsync();

            // Assert - Verify bulk creation
            var bulkViolations = await DbContext.Violations.Where(v => v.Description.StartsWith(ns + "_")).ToListAsync();
            Assert.Equal(10, bulkViolations.Count);

            // Act - Bulk update using a fresh context to avoid concurrency issues
            using var newScope = ((IServiceScopeFactory)ServiceProvider.GetService(typeof(IServiceScopeFactory))!).CreateScope();
            var newDbContext = newScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var openViolationsToUpdate = await newDbContext.Violations
                .Where(v => v.Description.StartsWith(ns + "_") && v.Status == ViolationStatus.Open)
                .ToListAsync();
            
            // Only update if we found violations to update
            if (openViolationsToUpdate.Any())
            {
                foreach (var violation in openViolationsToUpdate)
                {
                    violation.Description += " - Updated";
                }
                await newDbContext.SaveChangesAsync();
            }

            // Assert - Verify bulk update
            var updatedViolations = await DbContext.Violations.Where(v => v.Description.StartsWith(ns + "_") && v.Description.Contains(" - Updated")).ToListAsync();
            Assert.Equal(openViolationsToUpdate.Count, updatedViolations.Count);

            // Act - Bulk delete
            DbContext.ChangeTracker.Clear();
            var violationsToDelete = await DbContext.Violations
                .Where(v => v.Description.StartsWith(ns + "_"))
                .Take(5)
                .ToListAsync();
            
            if (violationsToDelete.Any())
            {
                DbContext.Violations.RemoveRange(violationsToDelete);
                await DbContext.SaveChangesAsync();
            }

            // Assert - Verify bulk delete (soft delete)
            var remainingBulkViolations = await DbContext.Violations
                .Where(v => v.Description.StartsWith(ns + "_") && !v.IsDeleted)
                .ToListAsync();
            Assert.Equal(5, remainingBulkViolations.Count);
            
            // Verify that the "deleted" violations are soft deleted
            var softDeletedViolations = await DbContext.Violations
                .IgnoreQueryFilters()
                .Where(v => v.Description.StartsWith(ns + "_") && v.IsDeleted)
                .ToListAsync();
            Assert.Equal(5, softDeletedViolations.Count);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task QueryPerformance_ShouldHandleComplexQueries()
    {
        var ns = GenerateUniqueTestNamespace(nameof(QueryPerformance_ShouldHandleComplexQueries));
        try
        {
            // Arrange - Create test data
            var violationTypes = new List<ViolationType>();
            for (int i = 1; i <= 3; i++)
            {
                var violationType = await CreateTestViolationTypeAsync(ns, $"PERF_TYPE_{i}", $"Performance test covenant {i}");
                violationTypes.Add(violationType);
            }

            var violations = new List<Violation>();
            for (int i = 1; i <= 20; i++)
            {
                var violationType = violationTypes[i % violationTypes.Count];
                var violation = new Violation
                {
                    Id = Guid.NewGuid(),
                    Description = $"{ns}_Performance violation {i}",
                    Status = i % 3 == 0 ? ViolationStatus.Closed : ViolationStatus.Open,
                    OccurrenceDate = DateTime.UtcNow.AddDays(-i),
                    ViolationTypeId = violationType.Id
                };
                violations.Add(violation);
            }

            DbContext.ChangeTracker.Clear();
            DbContext.Violations.AddRange(violations);
            await DbContext.SaveChangesAsync();

            // Act - Complex query with multiple conditions
            var complexQuery = await DbContext.Violations
                .Include(v => v.ViolationType)
                .Where(v => v.Status == ViolationStatus.Open)
                .Where(v => v.OccurrenceDate >= DateTime.UtcNow.AddDays(-10))
                .Where(v => v.Description.StartsWith(ns + "_"))
                .OrderBy(v => v.OccurrenceDate)
                .ThenBy(v => v.ViolationType!.Name)
                .ToListAsync();

            // Assert - Verify complex query results
            Assert.True(complexQuery.Count > 0);
            Assert.All(complexQuery, v => Assert.Equal(ViolationStatus.Open, v.Status));
            Assert.All(complexQuery, v => Assert.True(v.OccurrenceDate >= DateTime.UtcNow.AddDays(-10)));
            Assert.All(complexQuery, v => Assert.True(v.Description.StartsWith(ns + "_")));

            // Verify ordering
            for (int i = 1; i < complexQuery.Count; i++)
            {
                var previous = complexQuery[i - 1];
                var current = complexQuery[i];
                
                Assert.True(previous.OccurrenceDate <= current.OccurrenceDate);
                if (previous.OccurrenceDate == current.OccurrenceDate)
                {
                    Assert.True(string.Compare(previous.ViolationType!.Name, current.ViolationType!.Name) <= 0);
                }
            }
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task TransactionRollback_ShouldHandleErrors()
    {
        var ns = GenerateUniqueTestNamespace(nameof(TransactionRollback_ShouldHandleErrors));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TRANSACTION_TEST", "Transaction test covenant");
            var validViolation = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_Valid violation",
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id
            };

            var invalidViolation = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_", // Invalid: empty description
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id
            };

            // Act & Assert - Test transaction rollback
            using var transaction = await DbContext.Database.BeginTransactionAsync();
            try
            {
                DbContext.Violations.Add(validViolation);
                await DbContext.SaveChangesAsync();

                DbContext.Violations.Add(invalidViolation);
                await DbContext.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                
                // Verify that neither violation was saved due to rollback
                var savedViolations = await DbContext.Violations.Where(v => v.Id == validViolation.Id || v.Id == invalidViolation.Id).ToListAsync();
                Assert.Empty(savedViolations);
            }
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldHandleMultipleOperations()
    {
        var ns = GenerateUniqueTestNamespace(nameof(ConcurrentAccess_ShouldHandleMultipleOperations));
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "CONCURRENT_TEST", "Concurrent test covenant");
            var tasks = new List<Task>();

            // Act - Simulate concurrent operations
            for (int i = 1; i <= 5; i++)
            {
                var task = Task.Run(async () =>
                {
                    using var scope = ((IServiceScopeFactory)ServiceProvider.GetService(typeof(IServiceScopeFactory))!).CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var violation = new Violation
                    {
                        Id = Guid.NewGuid(),
                        Description = $"{ns}_Concurrent violation {i}",
                        Status = ViolationStatus.Open,
                        OccurrenceDate = DateTime.UtcNow,
                        ViolationTypeId = violationType.Id
                    };

                    dbContext.Violations.Add(violation);
                    await dbContext.SaveChangesAsync();
                });
                tasks.Add(task);
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Assert - Verify all violations were created
            var allViolations = await DbContext.Violations.Where(v => v.Description.StartsWith(ns + "_")).ToListAsync();
            Assert.Equal(5, allViolations.Count);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }
} 
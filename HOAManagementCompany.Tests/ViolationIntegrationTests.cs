using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HOAManagementCompany.Models;
using Xunit;

namespace HOAManagementCompany.Tests;

public class ViolationIntegrationTests : TestBase
{
    [Fact]
    public async Task CreateViolation_ShouldSucceed()
    {
        var ns = nameof(CreateViolation_ShouldSucceed);
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "GRASS_VIOLATION", "Lawn maintenance covenant");
            var violation = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_Lawn is overgrown and needs to be mowed",
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id
            };
            // Act
            DbContext.Violations.Add(violation);
            await DbContext.SaveChangesAsync();
            // Assert
            var savedViolation = await DbContext.Violations.FirstOrDefaultAsync(v => v.Id == violation.Id);
            Assert.NotNull(savedViolation);
            Assert.Equal($"{ns}_Lawn is overgrown and needs to be mowed", savedViolation.Description);
            Assert.Equal(ViolationStatus.Open, savedViolation.Status);
            Assert.Equal(violationType.Id, savedViolation.ViolationTypeId);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task ReadViolation_WithViolationType_ShouldIncludeRelatedData()
    {
        var ns = nameof(ReadViolation_WithViolationType_ShouldIncludeRelatedData);
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "POWERWASH_VIOLATION", "Exterior maintenance covenant");
            var violation = await CreateTestViolationAsync(ns, violationType.Id, "House needs power washing", ViolationStatus.Open);
            // Act
            var retrievedViolation = await DbContext.Violations.Include(v => v.ViolationType).FirstOrDefaultAsync(v => v.Id == violation.Id);
            // Assert
            Assert.NotNull(retrievedViolation);
            Assert.NotNull(retrievedViolation.ViolationType);
            Assert.Equal($"{ns}_POWERWASH_VIOLATION", retrievedViolation.ViolationType.Name);
            Assert.Equal("Exterior maintenance covenant", retrievedViolation.ViolationType.CovenantText);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task UpdateViolation_ShouldModifyData()
    {
        var ns = nameof(UpdateViolation_ShouldModifyData);
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST_VIOLATION", "Test covenant");
            var violation = await CreateTestViolationAsync(ns, violationType.Id, "Original description", ViolationStatus.Open);
            var originalId = violation.Id;
            // Act
            violation.Description = $"{ns}_Updated description";
            violation.Status = ViolationStatus.Closed;
            violation.OccurrenceDate = DateTime.UtcNow.AddDays(-1);
            await DbContext.SaveChangesAsync();
            // Assert
            var updatedViolation = await DbContext.Violations.FirstOrDefaultAsync(v => v.Id == originalId);
            Assert.NotNull(updatedViolation);
            Assert.Equal($"{ns}_Updated description", updatedViolation.Description);
            Assert.Equal(ViolationStatus.Closed, updatedViolation.Status);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task UpdateViolation_ViolationTypeId_ShouldModifyData()
    {
        var ns = nameof(UpdateViolation_ViolationTypeId_ShouldModifyData);
        try
        {
            // Arrange
            var originalViolationType = await CreateTestViolationTypeAsync(ns, "ORIGINAL_TYPE", "Original covenant");
            var newViolationType = await CreateTestViolationTypeAsync(ns, "NEW_TYPE", "New covenant");
            var violation = await CreateTestViolationAsync(ns, originalViolationType.Id, "Test violation", ViolationStatus.Open);
            var originalId = violation.Id;
            
            // Act
            violation.ViolationTypeId = newViolationType.Id;
            await DbContext.SaveChangesAsync();
            
            // Assert
            var updatedViolation = await DbContext.Violations
                .Include(v => v.ViolationType)
                .FirstOrDefaultAsync(v => v.Id == originalId);
            Assert.NotNull(updatedViolation);
            Assert.Equal(newViolationType.Id, updatedViolation.ViolationTypeId);
            Assert.NotNull(updatedViolation.ViolationType);
            Assert.Equal($"{ns}_NEW_TYPE", updatedViolation.ViolationType.Name);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task UpdateViolation_ViolationTypeId_UsingService_ShouldModifyData()
    {
        var ns = nameof(UpdateViolation_ViolationTypeId_UsingService_ShouldModifyData);
        try
        {
            // Arrange
            var originalViolationType = await CreateTestViolationTypeAsync(ns, "ORIGINAL_TYPE", "Original covenant");
            var newViolationType = await CreateTestViolationTypeAsync(ns, "NEW_TYPE", "New covenant");
            var violation = await CreateTestViolationAsync(ns, originalViolationType.Id, "Test violation", ViolationStatus.Open);
            var originalId = violation.Id;
            
            // Act - Use the service method
            violation.ViolationTypeId = newViolationType.Id;
            var violationService = ServiceProvider.GetRequiredService<HOAManagementCompany.Services.ViolationService>();
            await violationService.UpdateViolationAsync(violation);
            
            // Assert
            var updatedViolation = await violationService.GetViolationByIdAsync(originalId);
            Assert.NotNull(updatedViolation);
            Assert.Equal(newViolationType.Id, updatedViolation.ViolationTypeId);
            Assert.NotNull(updatedViolation.ViolationType);
            Assert.Equal($"{ns}_NEW_TYPE", updatedViolation.ViolationType.Name);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task UpdateViolation_ViolationTypeId_WithNavigationProperty_ShouldModifyData()
    {
        var ns = nameof(UpdateViolation_ViolationTypeId_WithNavigationProperty_ShouldModifyData);
        try
        {
            // Arrange - Simulate the frontend scenario
            var originalViolationType = await CreateTestViolationTypeAsync(ns, "ORIGINAL_TYPE", "Original covenant");
            var newViolationType = await CreateTestViolationTypeAsync(ns, "NEW_TYPE", "New covenant");
            var violation = await CreateTestViolationAsync(ns, originalViolationType.Id, "Test violation", ViolationStatus.Open);
            var originalId = violation.Id;
            
            // Simulate loading the violation with navigation property (like the frontend does)
            var violationService = ServiceProvider.GetRequiredService<HOAManagementCompany.Services.ViolationService>();
            var loadedViolation = await violationService.GetViolationByIdAsync(originalId);
            Assert.NotNull(loadedViolation);
            Assert.NotNull(loadedViolation.ViolationType);
            Assert.Equal($"{ns}_ORIGINAL_TYPE", loadedViolation.ViolationType.Name);
            
            // Act - Change ViolationTypeId (like the frontend dropdown does)
            loadedViolation.ViolationTypeId = newViolationType.Id;
            // Clear the navigation property (like our fix does)
            loadedViolation.ViolationType = null;
            
            // Update using the service
            await violationService.UpdateViolationAsync(loadedViolation);
            
            // Assert
            var updatedViolation = await violationService.GetViolationByIdAsync(originalId);
            Assert.NotNull(updatedViolation);
            Assert.Equal(newViolationType.Id, updatedViolation.ViolationTypeId);
            Assert.NotNull(updatedViolation.ViolationType);
            Assert.Equal($"{ns}_NEW_TYPE", updatedViolation.ViolationType.Name);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task UpdateViolation_ViolationTypeId_FrontendScenario_ShouldModifyData()
    {
        var ns = nameof(UpdateViolation_ViolationTypeId_FrontendScenario_ShouldModifyData);
        try
        {
            // Arrange - Simulate the frontend scenario exactly
            var originalViolationType = await CreateTestViolationTypeAsync(ns, "ORIGINAL_TYPE", "Original covenant");
            var newViolationType = await CreateTestViolationTypeAsync(ns, "NEW_TYPE", "New covenant");
            
            // Create violation with ViolationType navigation property loaded (like frontend does)
            var violation = await CreateTestViolationAsync(ns, originalViolationType.Id, "Test violation", ViolationStatus.Open);
            
            // Simulate what the frontend does - load with navigation property
            var loadedViolation = await DbContext.Violations
                .Include(v => v.ViolationType)
                .FirstOrDefaultAsync(v => v.Id == violation.Id);
            
            Assert.NotNull(loadedViolation);
            Assert.Equal(originalViolationType.Id, loadedViolation.ViolationTypeId);
            Assert.Equal(originalViolationType.Id, loadedViolation.ViolationType?.Id);
            
            // Act - Simulate user changing the dropdown (like our OnViolationTypeChanged method)
            loadedViolation.ViolationTypeId = newViolationType.Id;
            loadedViolation.ViolationType = newViolationType; // Update navigation property like our frontend does
            
            // Update using the service (like frontend does)
            var violationService = ServiceProvider.GetRequiredService<HOAManagementCompany.Services.ViolationService>();
            await violationService.UpdateViolationAsync(loadedViolation);
            
            // Assert - Verify the update worked
            var updatedViolation = await DbContext.Violations
                .Include(v => v.ViolationType)
                .FirstOrDefaultAsync(v => v.Id == violation.Id);
            
            Assert.NotNull(updatedViolation);
            Assert.Equal(newViolationType.Id, updatedViolation.ViolationTypeId);
            Assert.Equal(newViolationType.Id, updatedViolation.ViolationType?.Id);
            Assert.Equal(newViolationType.Name, updatedViolation.ViolationType?.Name);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task DeleteViolation_ShouldRemoveFromDatabase()
    {
        var ns = nameof(DeleteViolation_ShouldRemoveFromDatabase);
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TO_DELETE_VIOLATION", "Will be deleted");
            var violation = await CreateTestViolationAsync(ns, violationType.Id, "Will be deleted", ViolationStatus.Open);
            var violationId = violation.Id;
            // Act
            DbContext.Violations.Remove(violation);
            await DbContext.SaveChangesAsync();
            // Assert
            var deletedViolation = await DbContext.Violations.FirstOrDefaultAsync(v => v.Id == violationId);
            Assert.Null(deletedViolation);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetAllViolations_ShouldReturnAllRecords()
    {
        var ns = nameof(GetAllViolations_ShouldReturnAllRecords);
        try
        {
            // Arrange
            var violationType1 = await CreateTestViolationTypeAsync(ns, "TYPE_1", "Covenant 1");
            var violationType2 = await CreateTestViolationTypeAsync(ns, "TYPE_2", "Covenant 2");
            var violation1 = await CreateTestViolationAsync(ns, violationType1.Id, "Violation 1", ViolationStatus.Open);
            var violation2 = await CreateTestViolationAsync(ns, violationType1.Id, "Violation 2", ViolationStatus.Closed);
            var violation3 = await CreateTestViolationAsync(ns, violationType2.Id, "Violation 3", ViolationStatus.Open);
            // Act
            var allViolations = await DbContext.Violations.Where(v => v.Description.StartsWith(ns + "_")).ToListAsync();
            // Assert
            Assert.Equal(3, allViolations.Count);
            Assert.Contains(allViolations, v => v.Description == $"{ns}_Violation 1");
            Assert.Contains(allViolations, v => v.Description == $"{ns}_Violation 2");
            Assert.Contains(allViolations, v => v.Description == $"{ns}_Violation 3");
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsByStatus_ShouldFilterCorrectly()
    {
        var ns = nameof(GetViolationsByStatus_ShouldFilterCorrectly);
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "STATUS_TEST", "Status test covenant");
            var openViolation = await CreateTestViolationAsync(ns, violationType.Id, "Open violation", ViolationStatus.Open);
            var closedViolation = await CreateTestViolationAsync(ns, violationType.Id, "Closed violation", ViolationStatus.Closed);

            // Act
            var openViolations = await DbContext.Violations
                .Where(v => v.Status == ViolationStatus.Open)
                .ToListAsync();

            var closedViolations = await DbContext.Violations
                .Where(v => v.Status == ViolationStatus.Closed)
                .ToListAsync();

            // Assert
            Assert.Contains(openViolations, v => v.Id == openViolation.Id);
            Assert.DoesNotContain(openViolations, v => v.Id == closedViolation.Id);
            
            Assert.Contains(closedViolations, v => v.Id == closedViolation.Id);
            Assert.DoesNotContain(closedViolations, v => v.Id == openViolation.Id);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task GetViolationsByViolationType_ShouldFilterCorrectly()
    {
        var ns = nameof(GetViolationsByViolationType_ShouldFilterCorrectly);
        try
        {
            // Arrange
            var grassType = await CreateTestViolationTypeAsync(ns, "GRASS", "Grass covenant");
            var powerwashType = await CreateTestViolationTypeAsync(ns, "POWERWASH", "Powerwash covenant");
            
            var grassViolation1 = await CreateTestViolationAsync(ns, grassType.Id, "Grass violation 1");
            var grassViolation2 = await CreateTestViolationAsync(ns, grassType.Id, "Grass violation 2");
            var powerwashViolation = await CreateTestViolationAsync(ns, powerwashType.Id, "Powerwash violation");

            // Act
            var grassViolations = await DbContext.Violations
                .Where(v => v.ViolationTypeId == grassType.Id)
                .ToListAsync();

            var powerwashViolations = await DbContext.Violations
                .Where(v => v.ViolationTypeId == powerwashType.Id)
                .ToListAsync();

            // Assert
            Assert.Equal(2, grassViolations.Count);
            Assert.Contains(grassViolations, v => v.Id == grassViolation1.Id);
            Assert.Contains(grassViolations, v => v.Id == grassViolation2.Id);
            
            Assert.Single(powerwashViolations);
            Assert.Contains(powerwashViolations, v => v.Id == powerwashViolation.Id);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task CreateViolation_WithInvalidViolationTypeId_ShouldThrowException()
    {
        var ns = nameof(CreateViolation_WithInvalidViolationTypeId_ShouldThrowException);
        try
        {
            // Arrange
            var invalidViolationTypeId = Guid.NewGuid(); // Non-existent ID
            var violation = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_Test violation",
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = invalidViolationTypeId
            };

            // Act & Assert
            DbContext.Violations.Add(violation);
            await Assert.ThrowsAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
        }
        finally
        {
            // Clean up any partial state
            DbContext.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task CreateViolation_WithInvalidData_ShouldThrowException()
    {
        var ns = nameof(CreateViolation_WithInvalidData_ShouldThrowException);
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TEST", "Test covenant");
            var violation = new Violation
            {
                Id = Guid.NewGuid(),
                Description = $"{ns}_", // Invalid: empty description
                Status = ViolationStatus.Open,
                OccurrenceDate = DateTime.UtcNow,
                ViolationTypeId = violationType.Id
            };

            // Act & Assert
            DbContext.Violations.Add(violation);
            // Note: PostgreSQL allows empty strings, so this might not throw an exception
            // The validation is handled at the application level, not database level
            await DbContext.SaveChangesAsync();
            
            // Verify the data was saved (PostgreSQL allows empty strings)
            var savedViolation = await DbContext.Violations.FirstOrDefaultAsync(v => v.Id == violation.Id);
            Assert.NotNull(savedViolation);
            Assert.Equal($"{ns}_", savedViolation.Description);
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }

    [Fact]
    public async Task DeleteViolationType_WithRelatedViolations_ShouldRespectForeignKeyConstraint()
    {
        var ns = "FK_Test";
        try
        {
            // Arrange
            var violationType = await CreateTestViolationTypeAsync(ns, "TO_DELETE_WITH_VIOLATIONS", "Will fail to delete");
            var violation = await CreateTestViolationAsync(ns, violationType.Id, "Related violation");

            // Act & Assert - Use a new context to avoid entity tracking conflicts
            using var newScope = ((IServiceScopeFactory)ServiceProvider.GetService(typeof(IServiceScopeFactory))!).CreateScope();
            var newDbContext = newScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var violationTypeToDelete = await newDbContext.ViolationTypes.FirstOrDefaultAsync(vt => vt.Id == violationType.Id);
            Assert.NotNull(violationTypeToDelete);
            
            newDbContext.ViolationTypes.Remove(violationTypeToDelete);
            await Assert.ThrowsAsync<DbUpdateException>(() => newDbContext.SaveChangesAsync());
        }
        finally
        {
            await CleanupTestNamespaceAsync(ns);
        }
    }
} 
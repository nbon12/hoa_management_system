using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Xunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using HOAManagementCompany.Models;

namespace HOAManagementCompany.Tests;

public class AuditIntegrationTests : TestBase
{
    [Fact]
    public async Task Violation_ShouldHaveAuditColumns_OnCreation()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationAudit");
        var violationType = await CreateTestViolationTypeAsync(testNamespace, "TestType", "Test covenant text");
        
        var violation = new Violation
        {
            Id = Guid.NewGuid(),
            Description = $"{testNamespace}_Violation",
            Status = ViolationStatus.Open,
            OccurrenceDate = DateTime.UtcNow,
            ViolationTypeId = violationType.Id
        };

        // Act
        DbContext.Add(violation);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.NotEqual(DateTime.MinValue, violation.CreatedAt);
        Assert.NotEqual(DateTime.MinValue, violation.UpdatedAt);
        // Use tolerance for DateTime comparison due to microsecond precision differences
        Assert.True(Math.Abs((violation.CreatedAt - violation.UpdatedAt).TotalMilliseconds) < 100);
        // CreatedBy and UpdatedBy can be null when no user is authenticated in tests
        Assert.False(violation.IsDeleted);
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task ViolationType_ShouldHaveAuditColumns_OnCreation()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationTypeAudit");
        var violationType = new ViolationType
        {
            Id = Guid.NewGuid(),
            Name = "TestType",
            CovenantText = $"{testNamespace} covenant text"
        };

        // Act
        DbContext.Add(violationType);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.NotEqual(DateTime.MinValue, violationType.CreatedAt);
        Assert.NotEqual(DateTime.MinValue, violationType.UpdatedAt);
        // Use tolerance for DateTime comparison due to microsecond precision differences
        Assert.True(Math.Abs((violationType.CreatedAt - violationType.UpdatedAt).TotalMilliseconds) < 100);
        // CreatedBy and UpdatedBy can be null when no user is authenticated in tests
        Assert.False(violationType.IsDeleted);
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task Violation_ShouldUpdateAuditColumns_OnModification()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationUpdate");
        var violationType = await CreateTestViolationTypeAsync(testNamespace, "TestType", "Test covenant text");
        
        var violation = new Violation
        {
            Id = Guid.NewGuid(),
            Description = $"{testNamespace}_Violation",
            Status = ViolationStatus.Open,
            OccurrenceDate = DateTime.UtcNow,
            ViolationTypeId = violationType.Id
        };

        DbContext.Add(violation);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = violation.CreatedAt;
        var originalCreatedBy = violation.CreatedBy;
        var originalUpdatedAt = violation.UpdatedAt;

        // Act - Update the violation
        violation.Description = $"{testNamespace}_Updated_Violation";
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(originalCreatedAt, violation.CreatedAt); // CreatedAt should not change
        Assert.Equal(originalCreatedBy, violation.CreatedBy); // CreatedBy should not change
        Assert.True(violation.UpdatedAt > originalUpdatedAt); // UpdatedAt should be newer
        // UpdatedBy can be null when no user is authenticated in tests
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task ViolationType_ShouldUpdateAuditColumns_OnModification()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationTypeUpdate");
        var violationType = new ViolationType
        {
            Id = Guid.NewGuid(),
            Name = "TestType",
            CovenantText = "Test covenant text"
        };

        DbContext.Add(violationType);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = violationType.CreatedAt;
        var originalCreatedBy = violationType.CreatedBy;
        var originalUpdatedAt = violationType.UpdatedAt;

        // Act - Update the violation type
        violationType.Name = $"{testNamespace}_Updated_Type";
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(originalCreatedAt, violationType.CreatedAt); // CreatedAt should not change
        Assert.Equal(originalCreatedBy, violationType.CreatedBy); // CreatedBy should not change
        Assert.True(violationType.UpdatedAt > originalUpdatedAt); // UpdatedAt should be newer
        // UpdatedBy can be null when no user is authenticated in tests
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task Violation_ShouldApplySoftDelete_OnDeletion()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationSoftDelete");
        var violationType = await CreateTestViolationTypeAsync(testNamespace, "TestType", "Test covenant text");
        
        var violation = new Violation
        {
            Id = Guid.NewGuid(),
            Description = $"{testNamespace}_Violation",
            Status = ViolationStatus.Open,
            OccurrenceDate = DateTime.UtcNow,
            ViolationTypeId = violationType.Id
        };

        DbContext.Add(violation);
        await DbContext.SaveChangesAsync();

        var originalUpdatedAt = violation.UpdatedAt;

        // Act - Delete the violation
        DbContext.Remove(violation);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.True(violation.IsDeleted); // Should be soft deleted
        Assert.True(violation.UpdatedAt > originalUpdatedAt); // UpdatedAt should be newer
        // UpdatedBy can be null when no user is authenticated in tests
        
        // Verify it's still in the database but marked as deleted
        var deletedViolation = await DbContext.Violations.FindAsync(violation.Id);
        Assert.NotNull(deletedViolation);
        Assert.True(deletedViolation.IsDeleted);
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task ViolationType_ShouldApplySoftDelete_OnDeletion()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationTypeSoftDelete");
        var violationType = new ViolationType
        {
            Id = Guid.NewGuid(),
            Name = "TestType",
            CovenantText = "Test covenant text"
        };

        DbContext.Add(violationType);
        await DbContext.SaveChangesAsync();

        var originalUpdatedAt = violationType.UpdatedAt;

        // Act - Delete the violation type
        DbContext.Remove(violationType);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.True(violationType.IsDeleted); // Should be soft deleted
        Assert.True(violationType.UpdatedAt > originalUpdatedAt); // UpdatedAt should be newer
        // UpdatedBy can be null when no user is authenticated in tests
        
        // Verify it's still in the database but marked as deleted
        var deletedViolationType = await DbContext.ViolationTypes.FindAsync(violationType.Id);
        Assert.NotNull(deletedViolationType);
        Assert.True(deletedViolationType.IsDeleted);
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task Violation_ShouldPreserveCreatedAtAndCreatedBy_OnMultipleUpdates()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationMultipleUpdates");
        var violationType = await CreateTestViolationTypeAsync(testNamespace, "TestType", "Test covenant text");
        
        var violation = new Violation
        {
            Id = Guid.NewGuid(),
            Description = $"{testNamespace}_Violation",
            Status = ViolationStatus.Open,
            OccurrenceDate = DateTime.UtcNow,
            ViolationTypeId = violationType.Id
        };

        DbContext.Add(violation);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = violation.CreatedAt;
        var originalCreatedBy = violation.CreatedBy;

        // Act - Multiple updates
        violation.Description = $"{testNamespace}_Update1";
        await DbContext.SaveChangesAsync();

        violation.Status = ViolationStatus.Closed;
        await DbContext.SaveChangesAsync();

        violation.Description = $"{testNamespace}_Update2";
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(originalCreatedAt, violation.CreatedAt); // CreatedAt should not change
        Assert.Equal(originalCreatedBy, violation.CreatedBy); // CreatedBy should not change
        Assert.True(violation.UpdatedAt > originalCreatedAt); // UpdatedAt should be newer
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task ViolationType_ShouldPreserveCreatedAtAndCreatedBy_OnMultipleUpdates()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationTypeMultipleUpdates");
        var violationType = new ViolationType
        {
            Id = Guid.NewGuid(),
            Name = "TestType",
            CovenantText = "Test covenant text"
        };

        DbContext.Add(violationType);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = violationType.CreatedAt;
        var originalCreatedBy = violationType.CreatedBy;

        // Act - Multiple updates
        violationType.Name = $"{testNamespace}_Update1";
        await DbContext.SaveChangesAsync();

        violationType.CovenantText = $"{testNamespace} updated covenant text";
        await DbContext.SaveChangesAsync();

        violationType.Name = $"{testNamespace}_Update2";
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(originalCreatedAt, violationType.CreatedAt); // CreatedAt should not change
        Assert.Equal(originalCreatedBy, violationType.CreatedBy); // CreatedBy should not change
        Assert.True(violationType.UpdatedAt > originalCreatedAt); // UpdatedAt should be newer
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }
} 
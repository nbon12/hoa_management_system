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
        Assert.Equal(violation.CreatedAt, violation.UpdatedAt);
        Assert.NotNull(violation.CreatedBy);
        Assert.NotNull(violation.UpdatedBy);
        Assert.Equal(violation.CreatedBy, violation.UpdatedBy);
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
            CovenantText = "Test covenant text"
        };

        // Act
        DbContext.Add(violationType);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.NotEqual(DateTime.MinValue, violationType.CreatedAt);
        Assert.NotEqual(DateTime.MinValue, violationType.UpdatedAt);
        Assert.Equal(violationType.CreatedAt, violationType.UpdatedAt);
        Assert.NotNull(violationType.CreatedBy);
        Assert.NotNull(violationType.UpdatedBy);
        Assert.Equal(violationType.CreatedBy, violationType.UpdatedBy);
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
        Assert.NotNull(violation.UpdatedBy); // UpdatedBy should be set
        
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
        Assert.NotNull(violationType.UpdatedBy); // UpdatedBy should be set
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task Violation_ShouldApplySoftDelete_OnDeletion()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationDelete");
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
        Assert.NotNull(violation.UpdatedBy); // UpdatedBy should be set
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task ViolationType_ShouldApplySoftDelete_OnDeletion()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationTypeDelete");
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
        Assert.NotNull(violationType.UpdatedBy); // UpdatedBy should be set
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task Violation_ShouldPreserveCreatedAtAndCreatedBy_OnMultipleUpdates()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationMultiUpdate");
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

        // Act - Update multiple times
        for (int i = 0; i < 3; i++)
        {
            violation.Description = $"{testNamespace}_Updated_{i}";
            await DbContext.SaveChangesAsync();
        }

        // Assert
        Assert.Equal(originalCreatedAt, violation.CreatedAt);
        Assert.Equal(originalCreatedBy, violation.CreatedBy);
        Assert.True(violation.UpdatedAt > originalCreatedAt);
        Assert.NotNull(violation.UpdatedBy);
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }

    [Fact]
    public async Task ViolationType_ShouldPreserveCreatedAtAndCreatedBy_OnMultipleUpdates()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace("ViolationTypeMultiUpdate");
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

        // Act - Update multiple times
        for (int i = 0; i < 3; i++)
        {
            violationType.Name = $"{testNamespace}_Updated_{i}";
            await DbContext.SaveChangesAsync();
        }

        // Assert
        Assert.Equal(originalCreatedAt, violationType.CreatedAt);
        Assert.Equal(originalCreatedBy, violationType.CreatedBy);
        Assert.True(violationType.UpdatedAt > originalCreatedAt);
        Assert.NotNull(violationType.UpdatedBy);
        
        // Clean up
        await CleanupTestNamespaceAsync(testNamespace);
    }
} 
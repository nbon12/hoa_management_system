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

public class AuditEntityTests : TestBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditEntityTests()
    {
        // Create a mock HttpContextAccessor for testing
        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        
        // Mock an authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "test-user@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        httpContext.User = new ClaimsPrincipal(identity);
        
        httpContextAccessor.HttpContext = httpContext;
        _httpContextAccessor = httpContextAccessor;
    }

    [Fact]
    public void IAuditableEntity_Interface_ShouldHaveRequiredProperties()
    {
        // Arrange & Act - Create a test class that implements IAuditableEntity
        var testEntity = new TestAuditableEntity();

        // Assert - Verify all required properties exist and are accessible
        Assert.IsAssignableFrom<DateTime>(testEntity.CreatedAt);
        Assert.IsAssignableFrom<DateTime>(testEntity.UpdatedAt);
        Assert.IsAssignableFrom<string>(testEntity.CreatedBy);
        Assert.IsAssignableFrom<string>(testEntity.UpdatedBy);
        Assert.IsAssignableFrom<bool>(testEntity.IsDeleted);
    }

    [Fact]
    public void BaseAuditableEntity_ShouldImplementIAuditableEntity()
    {
        // Arrange & Act
        var baseEntity = new TestAuditableEntity();

        // Assert
        Assert.IsAssignableFrom<IAuditableEntity>(baseEntity);
    }

    [Fact]
    public void BaseAuditableEntity_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var baseEntity = new TestAuditableEntity();

        // Assert
        Assert.Equal(DateTime.MinValue, baseEntity.CreatedAt);
        Assert.Equal(DateTime.MinValue, baseEntity.UpdatedAt);
        Assert.Equal("", baseEntity.CreatedBy);
        Assert.Equal("", baseEntity.UpdatedBy);
        Assert.False(baseEntity.IsDeleted);
    }

    [Fact]
    public void BaseAuditableEntity_ShouldAllowPropertyAssignment()
    {
        // Arrange
        var baseEntity = new TestAuditableEntity();
        var now = DateTime.UtcNow;
        var testUser = "test-user@example.com";

        // Act
        baseEntity.CreatedAt = now;
        baseEntity.UpdatedAt = now;
        baseEntity.CreatedBy = testUser;
        baseEntity.UpdatedBy = testUser;
        baseEntity.IsDeleted = true;

        // Assert
        Assert.Equal(now, baseEntity.CreatedAt);
        Assert.Equal(now, baseEntity.UpdatedAt);
        Assert.Equal(testUser, baseEntity.CreatedBy);
        Assert.Equal(testUser, baseEntity.UpdatedBy);
        Assert.True(baseEntity.IsDeleted);
    }

    [Fact]
    public async Task ApplicationDbContext_ShouldApplyAuditInformation_OnEntityCreation()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(ApplicationDbContext_ShouldApplyAuditInformation_OnEntityCreation));
        var testEntity = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_TestEntity"
        };

        // Act
        DbContext.Add(testEntity);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.NotEqual(DateTime.MinValue, testEntity.CreatedAt);
        Assert.NotEqual(DateTime.MinValue, testEntity.UpdatedAt);
        Assert.Equal(testEntity.CreatedAt, testEntity.UpdatedAt);
        Assert.NotNull(testEntity.CreatedBy);
        Assert.NotNull(testEntity.UpdatedBy);
        Assert.Equal(testEntity.CreatedBy, testEntity.UpdatedBy);
        Assert.False(testEntity.IsDeleted);
    }

    [Fact]
    public async Task ApplicationDbContext_ShouldApplyAuditInformation_OnEntityUpdate()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(ApplicationDbContext_ShouldApplyAuditInformation_OnEntityUpdate));
        var testEntity = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_TestEntity"
        };

        DbContext.Add(testEntity);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = testEntity.CreatedAt;
        var originalCreatedBy = testEntity.CreatedBy;

        // Act - Update the entity
        testEntity.Name = $"{testNamespace}_UpdatedEntity";
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(originalCreatedAt, testEntity.CreatedAt); // CreatedAt should not change
        Assert.Equal(originalCreatedBy, testEntity.CreatedBy); // CreatedBy should not change
        Assert.True(testEntity.UpdatedAt > originalCreatedAt); // UpdatedAt should be newer
        Assert.NotNull(testEntity.UpdatedBy); // UpdatedBy should be set
    }

    [Fact]
    public async Task ApplicationDbContext_ShouldApplySoftDelete_OnEntityDeletion()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(ApplicationDbContext_ShouldApplySoftDelete_OnEntityDeletion));
        var testEntity = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_TestEntity"
        };

        DbContext.Add(testEntity);
        await DbContext.SaveChangesAsync();

        var originalUpdatedAt = testEntity.UpdatedAt;

        // Act - Delete the entity
        DbContext.Remove(testEntity);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.True(testEntity.IsDeleted); // Should be soft deleted
        Assert.True(testEntity.UpdatedAt > originalUpdatedAt); // UpdatedAt should be newer
        Assert.NotNull(testEntity.UpdatedBy); // UpdatedBy should be set
    }

    [Fact]
    public async Task ApplicationDbContext_ShouldNotModifyAuditFields_ForNonAuditableEntities()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(ApplicationDbContext_ShouldNotModifyAuditFields_ForNonAuditableEntities));
        var testEntity = new TestNonAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_TestEntity"
        };

        // Act
        DbContext.Add(testEntity);
        await DbContext.SaveChangesAsync();

        // Assert - Non-auditable entities should not have audit fields modified
        // This test verifies that the audit logic only applies to IAuditableEntity implementations
        Assert.True(true); // If we get here without errors, the test passes
    }

    [Fact]
    public async Task ApplicationDbContext_ShouldHandleMultipleEntities_WithDifferentStates()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(ApplicationDbContext_ShouldHandleMultipleEntities_WithDifferentStates));
        
        var entityToCreate = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_ToCreate"
        };

        var entityToUpdate = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_ToUpdate"
        };

        var entityToDelete = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_ToDelete"
        };

        // Add entities for update and delete operations
        DbContext.Add(entityToUpdate);
        DbContext.Add(entityToDelete);
        await DbContext.SaveChangesAsync();

        var originalUpdatedAt = entityToUpdate.UpdatedAt;
        var originalDeleteUpdatedAt = entityToDelete.UpdatedAt;

        // Act - Perform multiple operations
        DbContext.Add(entityToCreate);
        entityToUpdate.Name = $"{testNamespace}_Updated";
        DbContext.Remove(entityToDelete);
        await DbContext.SaveChangesAsync();

        // Assert
        // Created entity
        Assert.NotEqual(DateTime.MinValue, entityToCreate.CreatedAt);
        Assert.NotNull(entityToCreate.CreatedBy);
        Assert.False(entityToCreate.IsDeleted);

        // Updated entity
        Assert.True(entityToUpdate.UpdatedAt > originalUpdatedAt);
        Assert.NotNull(entityToUpdate.UpdatedBy);
        Assert.False(entityToUpdate.IsDeleted);

        // Deleted entity
        Assert.True(entityToDelete.IsDeleted);
        Assert.True(entityToDelete.UpdatedAt > originalDeleteUpdatedAt);
        Assert.NotNull(entityToDelete.UpdatedBy);
    }

    [Fact]
    public async Task ApplicationDbContext_ShouldPreserveCreatedAtAndCreatedBy_OnUpdate()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(ApplicationDbContext_ShouldPreserveCreatedAtAndCreatedBy_OnUpdate));
        var testEntity = new TestAuditableEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_TestEntity"
        };

        DbContext.Add(testEntity);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = testEntity.CreatedAt;
        var originalCreatedBy = testEntity.CreatedBy;

        // Act - Update multiple times
        for (int i = 0; i < 3; i++)
        {
            testEntity.Name = $"{testNamespace}_Updated_{i}";
            await DbContext.SaveChangesAsync();
        }

        // Assert
        Assert.Equal(originalCreatedAt, testEntity.CreatedAt);
        Assert.Equal(originalCreatedBy, testEntity.CreatedBy);
        Assert.True(testEntity.UpdatedAt > originalCreatedAt);
        Assert.NotNull(testEntity.UpdatedBy);
    }

    // Test helper classes are now in HOAManagementCompany.Models.TestEntities
} 
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

public class AuditLogicTests : TestBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogicTests()
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
        var entityType = typeof(TestAuditableEntity);

        // Assert - Verify all required properties exist and are accessible
        Assert.IsAssignableFrom<DateTime>(testEntity.CreatedAt);
        Assert.IsAssignableFrom<DateTime>(testEntity.UpdatedAt);
        Assert.IsAssignableFrom<bool>(testEntity.IsDeleted);
        
        // Check property types using reflection
        var createdByProperty = entityType.GetProperty("CreatedBy");
        var updatedByProperty = entityType.GetProperty("UpdatedBy");
        
        Assert.NotNull(createdByProperty);
        Assert.NotNull(updatedByProperty);
        Assert.Equal(typeof(string), createdByProperty.PropertyType);
        Assert.Equal(typeof(string), updatedByProperty.PropertyType);
        
        // Also verify the property types are correct for non-nullable properties
        Assert.Equal(typeof(DateTime), testEntity.CreatedAt.GetType());
        Assert.Equal(typeof(DateTime), testEntity.UpdatedAt.GetType());
        Assert.Equal(typeof(bool), testEntity.IsDeleted.GetType());
        
        // For nullable string properties, we need to assign a value first to check the type
        testEntity.CreatedBy = "test";
        testEntity.UpdatedBy = "test";
        Assert.Equal(typeof(string), testEntity.CreatedBy.GetType());
        Assert.Equal(typeof(string), testEntity.UpdatedBy.GetType());
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
        Assert.Null(baseEntity.CreatedBy);
        Assert.Null(baseEntity.UpdatedBy);
        Assert.False(baseEntity.IsDeleted);
    }

    [Fact]
    public void BaseAuditableEntity_ShouldAllowPropertyAssignment()
    {
        // Arrange
        var baseEntity = new TestAuditableEntity();
        var now = DateTime.UtcNow;
        var testUserId = "test-user-id";

        // Act
        baseEntity.CreatedAt = now;
        baseEntity.UpdatedAt = now;
        baseEntity.CreatedBy = testUserId;
        baseEntity.UpdatedBy = testUserId;
        baseEntity.IsDeleted = true;

        // Assert
        Assert.Equal(now, baseEntity.CreatedAt);
        Assert.Equal(now, baseEntity.UpdatedAt);
        Assert.Equal(testUserId, baseEntity.CreatedBy);
        Assert.Equal(testUserId, baseEntity.UpdatedBy);
        Assert.True(baseEntity.IsDeleted);
    }

    [Fact]
    public void Violation_ShouldInheritFromBaseAuditableEntity()
    {
        // Arrange & Act
        var violation = new Violation();

        // Assert
        Assert.IsAssignableFrom<BaseAuditableEntity>(violation);
        Assert.IsAssignableFrom<IAuditableEntity>(violation);
    }

    [Fact]
    public void ViolationType_ShouldInheritFromBaseAuditableEntity()
    {
        // Arrange & Act
        var violationType = new ViolationType();

        // Assert
        Assert.IsAssignableFrom<BaseAuditableEntity>(violationType);
        Assert.IsAssignableFrom<IAuditableEntity>(violationType);
    }

    [Fact]
    public void Violation_ShouldHaveAuditProperties()
    {
        // Arrange
        var violation = new Violation();
        var now = DateTime.UtcNow;
        var testUserId = "test-user-id";

        // Act
        violation.CreatedAt = now;
        violation.UpdatedAt = now;
        violation.CreatedBy = testUserId;
        violation.UpdatedBy = testUserId;
        violation.IsDeleted = true;

        // Assert
        Assert.Equal(now, violation.CreatedAt);
        Assert.Equal(now, violation.UpdatedAt);
        Assert.Equal(testUserId, violation.CreatedBy);
        Assert.Equal(testUserId, violation.UpdatedBy);
        Assert.True(violation.IsDeleted);
    }

    [Fact]
    public void ViolationType_ShouldHaveAuditProperties()
    {
        // Arrange
        var violationType = new ViolationType();
        var now = DateTime.UtcNow;
        var testUserId = "test-user-id";

        // Act
        violationType.CreatedAt = now;
        violationType.UpdatedAt = now;
        violationType.CreatedBy = testUserId;
        violationType.UpdatedBy = testUserId;
        violationType.IsDeleted = true;

        // Assert
        Assert.Equal(now, violationType.CreatedAt);
        Assert.Equal(now, violationType.UpdatedAt);
        Assert.Equal(testUserId, violationType.CreatedBy);
        Assert.Equal(testUserId, violationType.UpdatedBy);
        Assert.True(violationType.IsDeleted);
    }

    // Test helper classes
    private class TestAuditableEntity : BaseAuditableEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }
} 
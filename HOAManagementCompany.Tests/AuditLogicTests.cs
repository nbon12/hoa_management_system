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
        var testUser = "test-user@example.com";

        // Act
        violation.CreatedAt = now;
        violation.UpdatedAt = now;
        violation.CreatedBy = testUser;
        violation.UpdatedBy = testUser;
        violation.IsDeleted = true;

        // Assert
        Assert.Equal(now, violation.CreatedAt);
        Assert.Equal(now, violation.UpdatedAt);
        Assert.Equal(testUser, violation.CreatedBy);
        Assert.Equal(testUser, violation.UpdatedBy);
        Assert.True(violation.IsDeleted);
    }

    [Fact]
    public void ViolationType_ShouldHaveAuditProperties()
    {
        // Arrange
        var violationType = new ViolationType();
        var now = DateTime.UtcNow;
        var testUser = "test-user@example.com";

        // Act
        violationType.CreatedAt = now;
        violationType.UpdatedAt = now;
        violationType.CreatedBy = testUser;
        violationType.UpdatedBy = testUser;
        violationType.IsDeleted = true;

        // Assert
        Assert.Equal(now, violationType.CreatedAt);
        Assert.Equal(now, violationType.UpdatedAt);
        Assert.Equal(testUser, violationType.CreatedBy);
        Assert.Equal(testUser, violationType.UpdatedBy);
        Assert.True(violationType.IsDeleted);
    }

    // Test helper classes
    private class TestAuditableEntity : BaseAuditableEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }
} 
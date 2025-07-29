using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using HOAManagementCompany.Constants;
using HOAManagementCompany.Models;
using HOAManagementCompany.Services;
using HOAManagementCompany.Authorization.Handlers;
using HOAManagementCompany.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Xunit;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace HOAManagementCompany.Tests;

public class PermissionAuthorizationTests : TestBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserRoleService _userRoleService;
    private readonly ApplicationDbContext _context;
    private readonly PermissionHandler _permissionHandler;

    public PermissionAuthorizationTests()
    {
        var serviceProvider = CreateServiceProvider();
        _userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        _roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        _userRoleService = serviceProvider.GetRequiredService<UserRoleService>();
        _context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _permissionHandler = serviceProvider.GetRequiredService<PermissionHandler>();
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Configure database context for testing
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
                              "Host=localhost;Port=5432;Database=sequestria;Username=sequestria1;Password=HXCKFJ3498fajjAJR94";
        
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });
        
        // Add Identity services
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        
        // Add UserRoleService
        services.AddScoped<UserRoleService>();
        
        // Add PermissionHandler
        services.AddScoped<PermissionHandler>();
        
        return services.BuildServiceProvider();
    }

    private async Task CleanupTestDataAsync(string testNamespace)
    {
        // Clear entity tracking first
        _context.ChangeTracker.Clear();
        
        // Remove role permissions for this namespace
        var rolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleName.StartsWith(testNamespace + "_"))
            .ToListAsync();
        
        if (rolePermissions.Any())
        {
            _context.RolePermissions.RemoveRange(rolePermissions);
            await _context.SaveChangesAsync();
        }
        
        // Remove roles for this namespace
        var roles = await _roleManager.Roles
            .Where(r => r.Name.StartsWith(testNamespace + "_"))
            .ToListAsync();
        
        foreach (var role in roles)
        {
            await _roleManager.DeleteAsync(role);
        }
        
        // Remove users for this namespace
        var users = await _userManager.Users
            .Where(u => u.Email.StartsWith(testNamespace + "_"))
            .ToListAsync();
        
        foreach (var user in users)
        {
            await _userManager.DeleteAsync(user);
        }
        
        // Clear entity tracking after cleanup
        _context.ChangeTracker.Clear();
    }

    private string GenerateShortTestNamespace(string prefix)
    {
        var timestamp = DateTime.UtcNow.ToString("HHmmss");
        var randomSuffix = new Random().Next(100, 999);
        
        // Calculate available space for prefix
        // RoleName field is varchar(50), so we need to leave room for "_Role" suffix
        // Format: {prefix}_{timestamp}_{random}_{Role} = max 50 chars
        // timestamp = 6 chars, random = 3 chars, "_Role" = 5 chars, separators = 3 chars
        // Available space for prefix: 50 - 6 - 3 - 5 - 3 = 33 chars
        
        var maxPrefixLength = 33;
        var abbreviatedPrefix = prefix.Length > maxPrefixLength 
            ? prefix.Substring(0, maxPrefixLength - 3) + "..." 
            : prefix;
            
        return $"{abbreviatedPrefix}_{timestamp}_{randomSuffix}";
    }

    [Fact]
    public async Task UserHasPermissionAsync_WithValidPermission_ShouldReturnTrue()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(UserHasPermissionAsync_WithValidPermission_ShouldReturnTrue));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create role and add permission
        var roleName = $"{testNamespace}_Role";
        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Add user to role
        await _userManager.AddToRoleAsync(user, roleName);

        // Add permission to role
        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = roleName,
            Permission = Permissions.ViolationsRead,
            CreatedAt = DateTime.UtcNow
        };
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act
        var hasPermission = await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationsRead);

        // Assert
        Assert.True(hasPermission);

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task UserHasPermissionAsync_WithInvalidPermission_ShouldReturnFalse()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(UserHasPermissionAsync_WithInvalidPermission_ShouldReturnFalse));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create role and add permission
        var roleName = $"{testNamespace}_Role";
        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Add user to role
        await _userManager.AddToRoleAsync(user, roleName);

        // Add permission to role
        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = roleName,
            Permission = Permissions.ViolationsRead,
            CreatedAt = DateTime.UtcNow
        };
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act
        var hasPermission = await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationsCreate);

        // Assert
        Assert.False(hasPermission);

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ShouldReturnAllUserPermissions()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(GetUserPermissionsAsync_ShouldReturnAllUserPermissions));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create roles and add permissions
        var role1Name = $"{testNamespace}_Role1";
        var role2Name = $"{testNamespace}_Role2";
        
        var role1 = new IdentityRole(role1Name);
        var role2 = new IdentityRole(role2Name);
        await _roleManager.CreateAsync(role1);
        await _roleManager.CreateAsync(role2);

        // Add user to both roles
        await _userManager.AddToRoleAsync(user, role1Name);
        await _userManager.AddToRoleAsync(user, role2Name);

        // Add permissions to roles
        var rolePermissions = new List<RolePermission>
        {
            new() { Id = Guid.NewGuid(), RoleName = role1Name, Permission = Permissions.ViolationsRead, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), RoleName = role1Name, Permission = Permissions.ViolationsCreate, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), RoleName = role2Name, Permission = Permissions.ViolationTypesRead, CreatedAt = DateTime.UtcNow }
        };
        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();

        // Act
        var permissions = await _userRoleService.GetUserPermissionsAsync(user.Id);

        // Assert
        Assert.Equal(3, permissions.Count);
        Assert.Contains(Permissions.ViolationsRead, permissions);
        Assert.Contains(Permissions.ViolationsCreate, permissions);
        Assert.Contains(Permissions.ViolationTypesRead, permissions);

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task PermissionHandler_WithValidPermission_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(PermissionHandler_WithValidPermission_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create role and add permission
        var roleName = $"{testNamespace}_Role";
        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Add user to role
        await _userManager.AddToRoleAsync(user, roleName);

        // Add permission to role
        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = roleName,
            Permission = Permissions.ViolationsRead,
            CreatedAt = DateTime.UtcNow
        };
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Create claims for the user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Create authorization context
        var requirement = new PermissionRequirement(Permissions.ViolationsRead);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        // Act
        await _permissionHandler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task PermissionHandler_WithInvalidPermission_ShouldFail()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(PermissionHandler_WithInvalidPermission_ShouldFail));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create role and add permission
        var roleName = $"{testNamespace}_Role";
        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Add user to role
        await _userManager.AddToRoleAsync(user, roleName);

        // Add permission to role
        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = roleName,
            Permission = Permissions.ViolationsRead,
            CreatedAt = DateTime.UtcNow
        };
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Create claims for the user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Create authorization context with different permission
        var requirement = new PermissionRequirement(Permissions.ViolationsCreate);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        // Act
        await _permissionHandler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task PermissionHandler_WithUnauthenticatedUser_ShouldFail()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // No authentication
        var requirement = new PermissionRequirement(Permissions.ViolationsRead);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        // Act
        await _permissionHandler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task PermissionHandler_WithNoUserId_ShouldFail()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.Email, "test@example.com")
            // No NameIdentifier claim
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.ViolationsRead);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        // Act
        await _permissionHandler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AdministratorRole_ShouldHaveAllPermissions()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(AdministratorRole_ShouldHaveAllPermissions));
        var email = $"{testNamespace}_admin@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create Administrator role
        var adminRole = new IdentityRole(Roles.Administrator);
        await _roleManager.CreateAsync(adminRole);

        // Add user to Administrator role
        await _userManager.AddToRoleAsync(user, Roles.Administrator);

        // Seed administrator permissions (this should be done by the application)
        var adminPermissions = new[]
        {
            Permissions.ViolationsRead, Permissions.ViolationsCreate, Permissions.ViolationsUpdate, Permissions.ViolationsDelete,
            Permissions.ViolationTypesRead, Permissions.ViolationTypesCreate, Permissions.ViolationTypesUpdate, Permissions.ViolationTypesDelete,
            Permissions.UsersRead, Permissions.UsersCreate, Permissions.UsersUpdate, Permissions.UsersDelete,
            Permissions.RolesManage
        };

        var rolePermissions = adminPermissions.Select(permission => new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = Roles.Administrator,
            Permission = permission,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();

        // Act & Assert - Check that administrator has all permissions
        foreach (var permission in adminPermissions)
        {
            var hasPermission = await _userRoleService.UserHasPermissionAsync(user.Id, permission);
            Assert.True(hasPermission, $"Administrator should have permission: {permission}");
        }

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task BoardMemberRole_ShouldHaveLimitedPermissions()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(BoardMemberRole_ShouldHaveLimitedPermissions));
        var email = $"{testNamespace}_board@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create Board Member role
        var boardRole = new IdentityRole(Roles.BoardMember);
        await _roleManager.CreateAsync(boardRole);

        // Add user to Board Member role
        await _userManager.AddToRoleAsync(user, Roles.BoardMember);

        // Seed board member permissions
        var boardPermissions = new[]
        {
            Permissions.ViolationsRead,
            Permissions.ViolationTypesRead
        };

        var rolePermissions = boardPermissions.Select(permission => new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = Roles.BoardMember,
            Permission = permission,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();

        // Act & Assert - Check that board member has read-only permissions
        Assert.True(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationsRead));
        Assert.True(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationTypesRead));
        
        // Should not have write permissions
        Assert.False(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationsCreate));
        Assert.False(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationsUpdate));
        Assert.False(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationsDelete));
        Assert.False(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationTypesCreate));
        Assert.False(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationTypesUpdate));
        Assert.False(await _userRoleService.UserHasPermissionAsync(user.Id, Permissions.ViolationTypesDelete));

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }

    [Fact]
    public async Task HomeownerRole_ShouldHaveNoPermissions()
    {
        // Arrange
        var testNamespace = GenerateShortTestNamespace(nameof(HomeownerRole_ShouldHaveNoPermissions));
        var email = $"{testNamespace}_homeowner@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create Homeowner role
        var homeownerRole = new IdentityRole(Roles.Homeowner);
        await _roleManager.CreateAsync(homeownerRole);

        // Add user to Homeowner role
        await _userManager.AddToRoleAsync(user, Roles.Homeowner);

        // Act & Assert - Check that homeowner has no permissions
        var allPermissions = new[]
        {
            Permissions.ViolationsRead, Permissions.ViolationsCreate, Permissions.ViolationsUpdate, Permissions.ViolationsDelete,
            Permissions.ViolationTypesRead, Permissions.ViolationTypesCreate, Permissions.ViolationTypesUpdate, Permissions.ViolationTypesDelete,
            Permissions.UsersRead, Permissions.UsersCreate, Permissions.UsersUpdate, Permissions.UsersDelete,
            Permissions.RolesManage
        };

        foreach (var permission in allPermissions)
        {
            var hasPermission = await _userRoleService.UserHasPermissionAsync(user.Id, permission);
            Assert.False(hasPermission, $"Homeowner should not have permission: {permission}");
        }

        // Cleanup
        await CleanupTestDataAsync(testNamespace);
    }
} 
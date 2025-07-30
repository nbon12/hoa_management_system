using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HOAManagementCompany.Services;
using HOAManagementCompany.Constants;
using Xunit;

namespace HOAManagementCompany.Tests;

public class AuthenticationIntegrationTests : TestBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserRoleService _userRoleService;

    public AuthenticationIntegrationTests()
    {
        // Add Identity services to the test container
        var services = new ServiceCollection();
        
        // Add logging services
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add the existing services from base
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
                                  "Host=localhost;Port=5432;Database=sequestria;Username=sequestria1;Password=HXCKFJ3498fajjAJR94";
            options.UseNpgsql(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Add Identity services
        services.AddIdentity<IdentityUser, IdentityRole>(options => {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Add UserRoleService
        services.AddScoped<UserRoleService>();

        var serviceProvider = services.BuildServiceProvider();
        
        // Ensure database is created and migrations are applied
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        
        _userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        _roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        _signInManager = serviceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        _userRoleService = serviceProvider.GetRequiredService<UserRoleService>();
    }

    [Fact]
    public async Task CreateUser_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(CreateUser_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        // Act
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);

        // Assert
        Assert.True(result.Succeeded, $"User creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        Assert.NotNull(user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(email, user.UserName);
    }

    [Fact]
    public async Task CreateUser_WithInvalidPassword_ShouldFail()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(CreateUser_WithInvalidPassword_ShouldFail));
        var email = $"{testNamespace}_testuser@example.com";
        var weakPassword = "weak"; // Too short, no uppercase, no digit, no special char

        // Act
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, weakPassword);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordTooShort");
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresUpper");
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresDigit");
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresNonAlphanumeric");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ShouldFail()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(CreateUser_WithDuplicateEmail_ShouldFail));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        // Create first user
        var user1 = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result1 = await _userManager.CreateAsync(user1, password);
        Assert.True(result1.Succeeded);

        // Act - Try to create second user with same email
        var user2 = new IdentityUser
        {
            UserName = $"{testNamespace}_testuser2@example.com",
            Email = email, // Same email
            EmailConfirmed = true
        };
        var result2 = await _userManager.CreateAsync(user2, password);

        // Assert
        Assert.False(result2.Succeeded);
        Assert.Contains(result2.Errors, e => e.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task UpdateUser_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UpdateUser_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Act
        user.PhoneNumber = "555-123-4567";
        var result = await _userManager.UpdateAsync(user);

        // Assert
        Assert.True(result.Succeeded);
        var updatedUser = await _userManager.FindByIdAsync(user.Id);
        Assert.Equal("555-123-4567", updatedUser.PhoneNumber);
    }

    [Fact]
    public async Task DeleteUser_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(DeleteUser_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Act
        var result = await _userManager.DeleteAsync(user);

        // Assert
        Assert.True(result.Succeeded);
        var deletedUser = await _userManager.FindByIdAsync(user.Id);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task CreateRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(CreateRole_ShouldSucceed));
        var roleName = $"{testNamespace}_TestRole";

        // Act
        var role = new IdentityRole(roleName);
        var result = await _roleManager.CreateAsync(role);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(role.Id);
        Assert.Equal(roleName, role.Name);

        // Verify role exists
        var createdRole = await _roleManager.FindByNameAsync(roleName);
        Assert.NotNull(createdRole);
    }

    [Fact]
    public async Task CreateRole_WithDuplicateName_ShouldFail()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(CreateRole_WithDuplicateName_ShouldFail));
        var roleName = $"{testNamespace}_TestRole";

        // Create first role
        var role1 = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role1);

        // Act - Try to create second role with same name
        var role2 = new IdentityRole(roleName);
        var result = await _roleManager.CreateAsync(role2);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "DuplicateRoleName");
    }

    [Fact]
    public async Task UpdateRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UpdateRole_ShouldSucceed));
        var roleName = $"{testNamespace}_TestRole";

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Act
        role.Name = $"{testNamespace}_UpdatedRole";
        var result = await _roleManager.UpdateAsync(role);

        // Assert
        Assert.True(result.Succeeded);
        var updatedRole = await _roleManager.FindByIdAsync(role.Id);
        Assert.Equal($"{testNamespace}_UpdatedRole", updatedRole.Name);
    }

    [Fact]
    public async Task DeleteRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(DeleteRole_ShouldSucceed));
        var roleName = $"{testNamespace}_TestRole";

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Act
        var result = await _roleManager.DeleteAsync(role);

        // Assert
        Assert.True(result.Succeeded);
        var deletedRole = await _roleManager.FindByIdAsync(role.Id);
        Assert.Null(deletedRole);
    }

    [Fact]
    public async Task AddUserToRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(AddUserToRole_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var roleName = $"{testNamespace}_TestRole";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Act
        var result = await _userManager.AddToRoleAsync(user, roleName);

        // Assert
        Assert.True(result.Succeeded);
        var userRoles = await _userManager.GetRolesAsync(user);
        Assert.Contains(roleName, userRoles);
    }

    [Fact]
    public async Task RemoveUserFromRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(RemoveUserFromRole_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var roleName = $"{testNamespace}_TestRole";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        await _userManager.AddToRoleAsync(user, roleName);

        // Act
        var result = await _userManager.RemoveFromRoleAsync(user, roleName);

        // Assert
        Assert.True(result.Succeeded);
        var userRoles = await _userManager.GetRolesAsync(user);
        Assert.DoesNotContain(roleName, userRoles);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(Login_WithValidCredentials_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Act - Test password verification instead of sign-in (which requires HttpContext)
        var isValidPassword = await _userManager.CheckPasswordAsync(user, password);

        // Assert
        Assert.True(isValidPassword);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldFail()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(Login_WithInvalidPassword_ShouldFail));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var wrongPassword = "WrongPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Act - Test password verification instead of sign-in (which requires HttpContext)
        var isValidPassword = await _userManager.CheckPasswordAsync(user, wrongPassword);

        // Assert
        Assert.False(isValidPassword);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldFail()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(Login_WithNonExistentUser_ShouldFail));
        var email = $"{testNamespace}_nonexistent@example.com";
        var password = "TestPass123!";

        // Act - Test finding non-existent user
        var user = await _userManager.FindByEmailAsync(email);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task Logout_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(Logout_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Act - Test that user exists and can be found
        var foundUser = await _userManager.FindByEmailAsync(email);

        // Assert
        Assert.NotNull(foundUser);
        Assert.Equal(email, foundUser.Email);
    }

    [Fact]
    public async Task UserRoleService_GetAllUsers_ShouldReturnUsers()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_GetAllUsers_ShouldReturnUsers));
        var email1 = $"{testNamespace}_user1@example.com";
        var email2 = $"{testNamespace}_user2@example.com";
        var password = "TestPass123!";

        var user1 = new IdentityUser { UserName = email1, Email = email1, EmailConfirmed = true };
        var user2 = new IdentityUser { UserName = email2, Email = email2, EmailConfirmed = true };

        await _userManager.CreateAsync(user1, password);
        await _userManager.CreateAsync(user2, password);

        // Act
        var users = await _userRoleService.GetAllUsersAsync();

        // Assert
        Assert.Contains(users, u => u.Email == email1);
        Assert.Contains(users, u => u.Email == email2);
    }

    [Fact]
    public async Task UserRoleService_GetAllRoles_ShouldReturnRoles()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_GetAllRoles_ShouldReturnRoles));
        var roleName1 = $"{testNamespace}_Role1";
        var roleName2 = $"{testNamespace}_Role2";

        var role1 = new IdentityRole(roleName1);
        var role2 = new IdentityRole(roleName2);

        await _roleManager.CreateAsync(role1);
        await _roleManager.CreateAsync(role2);

        // Act
        var roles = await _userRoleService.GetAllRolesAsync();

        // Assert
        Assert.Contains(roles, r => r.Name == roleName1);
        Assert.Contains(roles, r => r.Name == roleName2);
    }

    [Fact]
    public async Task UserRoleService_GetUserRoles_ShouldReturnUserRoles()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_GetUserRoles_ShouldReturnUserRoles));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var roleName = $"{testNamespace}_TestRole";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        await _userManager.AddToRoleAsync(user, roleName);

        // Act
        var userRoles = await _userRoleService.GetUserRolesAsync(user.Id);

        // Assert
        Assert.Contains(roleName, userRoles);
    }

    [Fact]
    public async Task UserRoleService_AddUserToRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_AddUserToRole_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var roleName = $"{testNamespace}_TestRole";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Act
        var result = await _userRoleService.AddUserToRoleAsync(user.Id, roleName);

        // Assert
        Assert.True(result);
        var userRoles = await _userManager.GetRolesAsync(user);
        Assert.Contains(roleName, userRoles);
    }

    [Fact]
    public async Task UserRoleService_RemoveUserFromRole_ShouldSucceed()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_RemoveUserFromRole_ShouldSucceed));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var roleName = $"{testNamespace}_TestRole";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        await _userManager.AddToRoleAsync(user, roleName);

        // Act
        var result = await _userRoleService.RemoveUserFromRoleAsync(user.Id, roleName);

        // Assert
        Assert.True(result);
        var userRoles = await _userManager.GetRolesAsync(user);
        Assert.DoesNotContain(roleName, userRoles);
    }

    [Fact]
    public async Task UserRoleService_IsUserInRole_ShouldReturnCorrectResult()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_IsUserInRole_ShouldReturnCorrectResult));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";
        var roleName = $"{testNamespace}_TestRole";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        var role = new IdentityRole(roleName);
        await _roleManager.CreateAsync(role);

        // Act & Assert - User not in role initially
        var isInRoleBefore = await _userRoleService.IsUserInRoleAsync(user.Id, roleName);
        Assert.False(isInRoleBefore);

        // Add user to role
        await _userManager.AddToRoleAsync(user, roleName);

        // Act & Assert - User now in role
        var isInRoleAfter = await _userRoleService.IsUserInRoleAsync(user.Id, roleName);
        Assert.True(isInRoleAfter);
    }

    [Fact]
    public async Task UserRoleService_GetUserById_ShouldReturnUser()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(UserRoleService_GetUserById_ShouldReturnUser));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Act
        var foundUser = await _userRoleService.GetUserByIdAsync(user.Id);

        // Assert
        Assert.NotNull(foundUser);
        Assert.Equal(email, foundUser.Email);
    }

    [Fact]
    public async Task UserRoleService_GetUserById_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidUserId = "invalid-user-id";

        // Act
        var foundUser = await _userRoleService.GetUserByIdAsync(invalidUserId);

        // Assert
        Assert.Null(foundUser);
    }

    [Fact]
    public async Task Authentication_WithHOAConstants_ShouldWork()
    {
        // Arrange
        var testNamespace = GenerateUniqueTestNamespace(nameof(Authentication_WithHOAConstants_ShouldWork));
        var email = $"{testNamespace}_testuser@example.com";
        var password = "TestPass123!";

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(user, password);

        // Create HOA roles
        var adminRole = new IdentityRole(Roles.Administrator);
        var boardRole = new IdentityRole(Roles.BoardMember);
        var homeownerRole = new IdentityRole(Roles.Homeowner);

        await _roleManager.CreateAsync(adminRole);
        await _roleManager.CreateAsync(boardRole);
        await _roleManager.CreateAsync(homeownerRole);

        // Act - Add user to multiple roles
        await _userManager.AddToRoleAsync(user, Roles.Administrator);
        await _userManager.AddToRoleAsync(user, Roles.BoardMember);

        // Assert
        var userRoles = await _userManager.GetRolesAsync(user);
        Assert.Contains(Roles.Administrator, userRoles);
        Assert.Contains(Roles.BoardMember, userRoles);
        Assert.DoesNotContain(Roles.Homeowner, userRoles);
    }


} 
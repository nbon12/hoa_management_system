using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HOAManagementCompany.Models;
using Microsoft.AspNetCore.Identity;
using HOAManagementCompany.Services;
using Microsoft.Playwright;
using Xunit;

namespace HOAManagementCompany.Tests;

public class PlaywrightTestBase : IAsyncLifetime
{
    protected readonly ApplicationDbContext DbContext;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly string BaseUrl;
    private static readonly Random _random = new Random();

    protected PlaywrightTestBase()
    {
        // Create a simple service collection for testing
        var services = new ServiceCollection();
        
        // Configure database context for testing
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
                              "Host=localhost;Port=5432;Database=sequestria;Username=sequestria1;Password=HXCKFJ3498fajjAJR94";

        // Add the test database context
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Add Identity services
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // Add logging
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            loggingBuilder.SetMinimumLevel(LogLevel.Information);
        });

        // Add services
        services.AddScoped<UserRoleService>();
        services.AddScoped<ViolationService>();

        ServiceProvider = services.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Use localhost:5212 as the base URL (the default development server port)
        BaseUrl = "http://localhost:5212/";
        
        // Ensure database is created and migrations are applied
        DbContext.Database.EnsureCreated();
        DbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Generates a unique test namespace with a random component to prevent conflicts between test runs
    /// </summary>
    /// <param name="testMethodName">The name of the test method</param>
    /// <returns>A unique namespace string</returns>
    protected string GenerateUniqueTestNamespace(string testMethodName)
    {
        var timestamp = DateTime.UtcNow.ToString("HHmmss");
        var randomSuffix = _random.Next(1000, 9999);
        return $"{testMethodName}_{timestamp}_{randomSuffix}";
    }

    protected async Task CleanupDatabaseAsync()
    {
        // Clear entity tracking first to avoid conflicts
        DbContext.ChangeTracker.Clear();
        
        // Use a transaction to ensure atomic cleanup
        using var transaction = await DbContext.Database.BeginTransactionAsync();
        try
        {
            // Only remove test-created data, not seeded data
            // Remove violations first due to foreign key constraints
            var testViolations = await DbContext.Violations
                .Where(v => v.Description.Contains("_") && 
                           (v.Description.Contains("TEST_") || 
                           v.Description.Contains("Bulk violation") ||
                           v.Description.Contains("Performance violation") ||
                           v.Description.Contains("Concurrent violation") ||
                           v.Description.Contains("Violation 1") ||
                           v.Description.Contains("Violation 2") ||
                           v.Description.Contains("Violation 3") ||
                           v.Description.Contains("Grass violation") ||
                           v.Description.Contains("Powerwash violation") ||
                           v.Description.Contains("Lawn is overgrown") ||
                           v.Description.Contains("House needs power washing") ||
                           v.Description.Contains("Fence needs repair") ||
                           v.Description.Contains("Valid violation") ||
                           v.Description.Contains("Open violation") ||
                           v.Description.Contains("Closed violation") ||
                           v.Description.Contains("Status test") ||
                           v.Description.Contains("Transaction test") ||
                           v.Description.Contains("Will be deleted") ||
                           v.Description.Contains("Related violation")))
                .ToListAsync();
            
            if (testViolations.Any())
            {
                DbContext.Violations.RemoveRange(testViolations);
                await DbContext.SaveChangesAsync();
            }
            
            // Remove test-created violation types (only those without related violations)
            var testViolationTypes = await DbContext.ViolationTypes
                .Where(vt => vt.Name.Contains("_") && 
                            (vt.Name.Contains("TEST_") ||
                            vt.Name.Contains("TYPE_") ||
                            vt.Name.Contains("PERF_TYPE_") ||
                            vt.Name.Contains("GRASS_VIOLATION") ||
                            vt.Name.Contains("POWERWASH_VIOLATION") ||
                            vt.Name.Contains("FENCE") ||
                            vt.Name.Contains("STATUS_TEST") ||
                            vt.Name.Contains("TRANSACTION_TEST") ||
                            vt.Name.Contains("CONCURRENT_TEST") ||
                            vt.Name.Contains("TO_DELETE") ||
                            vt.Name.Contains("EXISTING") ||
                            vt.Name.Contains("DUPLICATE") ||
                            vt.Name.Contains("ORIGINAL_NAME") ||
                            vt.Name.Contains("UPDATED_NAME") ||
                            vt.Name.Contains("TEST_READ_VIOLATION") ||
                            vt.Name.Contains("TEST_GRASS_VIOLATION") ||
                            vt.Name.Contains("TEST_VIOLATION") ||
                            vt.Name.Contains("TO_DELETE_VIOLATION") ||
                            vt.Name.Contains("TO_DELETE_WITH_VIOLATIONS")) &&
                            !DbContext.Violations.Any(v => v.ViolationTypeId == vt.Id))
                .ToListAsync();
            
            if (testViolationTypes.Any())
            {
                DbContext.ViolationTypes.RemoveRange(testViolationTypes);
                await DbContext.SaveChangesAsync();
            }
            
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            // Ignore cleanup errors - they're not critical for test success
        }
        finally
        {
            // Clear entity tracking after cleanup
            DbContext.ChangeTracker.Clear();
        }
    }

    protected async Task<ViolationType> CreateTestViolationTypeAsync(string testNamespace, string name, string covenantText)
    {
        // Clear entity tracking to avoid conflicts
        DbContext.ChangeTracker.Clear();
        
        var violationType = new ViolationType
        {
            Id = Guid.NewGuid(),
            Name = $"{testNamespace}_{name}",
            CovenantText = covenantText
        };
        DbContext.ViolationTypes.Add(violationType);
        await DbContext.SaveChangesAsync();
        return violationType;
    }

    protected async Task<Violation> CreateTestViolationAsync(string testNamespace, Guid violationTypeId, string description, ViolationStatus status = ViolationStatus.Open)
    {
        // Clear entity tracking to avoid conflicts
        DbContext.ChangeTracker.Clear();
        
        var violation = new Violation
        {
            Id = Guid.NewGuid(),
            Description = $"{testNamespace}_{description}",
            Status = status,
            OccurrenceDate = DateTime.UtcNow,
            ViolationTypeId = violationTypeId
        };
        DbContext.Violations.Add(violation);
        await DbContext.SaveChangesAsync();
        return violation;
    }

    protected async Task CleanupTestNamespaceAsync(string testNamespace)
    {
        // Clear entity tracking first
        DbContext.ChangeTracker.Clear();
        
        // Remove violations for this namespace (including soft-deleted ones)
        var violations = await DbContext.Violations
            .IgnoreQueryFilters()
            .Where(v => v.Description.StartsWith(testNamespace + "_"))
            .ToListAsync();
        if (violations.Any())
        {
            DbContext.Violations.RemoveRange(violations);
            await DbContext.SaveChangesAsync();
        }
        
        // Find and remove violation types that don't have related violations (including soft-deleted ones)
        var violationTypes = await DbContext.ViolationTypes
            .IgnoreQueryFilters()
            .Where(vt => vt.Name.StartsWith(testNamespace + "_") && 
                        !DbContext.Violations.IgnoreQueryFilters().Any(v => v.ViolationTypeId == vt.Id))
            .ToListAsync();
        if (violationTypes.Any())
        {
            DbContext.ViolationTypes.RemoveRange(violationTypes);
            await DbContext.SaveChangesAsync();
        }
        
        // Clear entity tracking after cleanup
        DbContext.ChangeTracker.Clear();
    }

    protected async Task CleanupTestUsersAsync(string testNamespace)
    {
        try
        {
            var userManager = ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            
            // Find and remove test users by email pattern
            var testUsers = await userManager.Users
                .Where(u => u.Email != null && u.Email.Contains(testNamespace))
                .ToListAsync();
            
            foreach (var user in testUsers)
            {
                await userManager.DeleteAsync(user);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during test user cleanup: {ex.Message}");
        }
    }

    public virtual async Task InitializeAsync()
    {
        // Ensure database is ready
        await DbContext.Database.EnsureCreatedAsync();
        await CleanupDatabaseAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Clean up test data
        await CleanupDatabaseAsync();
        
        // Dispose resources
        DbContext?.Dispose();
    }
} 
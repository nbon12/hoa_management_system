using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HOAManagementCompany.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using HOAManagementCompany.Services;

namespace HOAManagementCompany.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly ApplicationDbContext DbContext;
    protected readonly IServiceProvider ServiceProvider;
    private static readonly Random _random = new Random();
    protected HOAManagementCompany.Services.ViolationService ViolationService => ServiceProvider.GetRequiredService<HOAManagementCompany.Services.ViolationService>();

    protected TestBase()
    {
        var services = new ServiceCollection();
        
        // Configure database context for testing
        // Use environment variable for connection string in CI/CD, fallback to local development
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
                              "Host=localhost;Port=5432;Database=sequestria;Username=sequestria1;Password=HXCKFJ3498fajjAJR94";
        
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add services
        services.AddDbContextFactory<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });
        services.AddScoped<HOAManagementCompany.Services.ViolationService>();
        
        // Add Identity services
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
        
        services.AddScoped<UserRoleService>();

        ServiceProvider = services.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database is created and migrations are applied
        DbContext.Database.EnsureCreated();
        
        // Clear any existing entity tracking to ensure clean state
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
        
        // Remove violations for this namespace
        var violations = await DbContext.Violations.Where(v => v.Description.StartsWith(testNamespace + "_")).ToListAsync();
        if (violations.Any())
        {
            DbContext.Violations.RemoveRange(violations);
            await DbContext.SaveChangesAsync();
        }
        
        // Find and remove violation types that don't have related violations
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

    protected async Task<int> GetSeededViolationTypeCountAsync()
    {
        // Count all violation types that are not test-related
        return await DbContext.ViolationTypes
            .Where(vt => !vt.Name.Contains("TEST_") && 
                        !vt.Name.Contains("TYPE_") && 
                        !vt.Name.Contains("PERF_TYPE_") && 
                        !vt.Name.Contains("GRASS_VIOLATION") && 
                        !vt.Name.Contains("POWERWASH_VIOLATION") && 
                        !vt.Name.Contains("FENCE") && 
                        !vt.Name.Contains("STATUS_TEST") && 
                        !vt.Name.Contains("TRANSACTION_TEST") && 
                        !vt.Name.Contains("CONCURRENT_TEST") && 
                        !vt.Name.Contains("TO_DELETE") && 
                        !vt.Name.Contains("EXISTING") && 
                        !vt.Name.Contains("DUPLICATE") && 
                        !vt.Name.Contains("ORIGINAL_NAME") && 
                        !vt.Name.Contains("UPDATED_NAME") && 
                        !vt.Name.Contains("TEST_READ_VIOLATION") && 
                        !vt.Name.Contains("TEST_GRASS_VIOLATION") && 
                        !vt.Name.Contains("TEST_VIOLATION") && 
                        !vt.Name.Contains("TO_DELETE_VIOLATION") && 
                        !vt.Name.Contains("TO_DELETE_WITH_VIOLATIONS"))
            .CountAsync();
    }

    protected async Task EnsureCleanStateAsync()
    {
        // Clear any existing entity tracking to avoid conflicts
        DbContext.ChangeTracker.Clear();
        
        // Use raw SQL to bypass foreign key constraints for cleanup
        try
        {
            // First, disable foreign key checks temporarily
            await DbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = replica;");
            
            // Delete all test violations first
            await DbContext.Database.ExecuteSqlRawAsync(@"
                DELETE FROM ""Violations"" 
                WHERE ""Description"" LIKE '%TEST_%' 
                   OR ""Description"" LIKE '%Bulk violation%'
                   OR ""Description"" LIKE '%Performance violation%'
                   OR ""Description"" LIKE '%Concurrent violation%'
                   OR ""Description"" LIKE '%Violation 1%'
                   OR ""Description"" LIKE '%Violation 2%'
                   OR ""Description"" LIKE '%Violation 3%'
                   OR ""Description"" LIKE '%Grass violation%'
                   OR ""Description"" LIKE '%Powerwash violation%'
                   OR ""Description"" LIKE '%Lawn is overgrown%'
                   OR ""Description"" LIKE '%House needs power washing%'
                   OR ""Description"" LIKE '%Fence needs repair%'
                   OR ""Description"" LIKE '%Valid violation%'
                   OR ""Description"" LIKE '%Open violation%'
                   OR ""Description"" LIKE '%Closed violation%'
                   OR ""Description"" LIKE '%Status test%'
                   OR ""Description"" LIKE '%Transaction test%'
                   OR ""Description"" LIKE '%Will be deleted%'
                   OR ""Description"" LIKE '%Related violation%'
                   OR ""Description"" LIKE '%CompleteWorkflow%'
                   OR ""Description"" LIKE '%BulkOperationsTest%'");
            
            // Then delete all test violation types
            await DbContext.Database.ExecuteSqlRawAsync(@"
                DELETE FROM ""ViolationTypes"" 
                WHERE ""Name"" LIKE '%TEST_%'
                   OR ""Name"" LIKE '%TYPE_%'
                   OR ""Name"" LIKE '%PERF_TYPE_%'
                   OR ""Name"" LIKE '%GRASS_VIOLATION%'
                   OR ""Name"" LIKE '%POWERWASH_VIOLATION%'
                   OR ""Name"" LIKE '%FENCE%'
                   OR ""Name"" LIKE '%STATUS_TEST%'
                   OR ""Name"" LIKE '%TRANSACTION_TEST%'
                   OR ""Name"" LIKE '%CONCURRENT_TEST%'
                   OR ""Name"" LIKE '%TO_DELETE%'
                   OR ""Name"" LIKE '%EXISTING%'
                   OR ""Name"" LIKE '%DUPLICATE%'
                   OR ""Name"" LIKE '%ORIGINAL_NAME%'
                   OR ""Name"" LIKE '%UPDATED_NAME%'
                   OR ""Name"" LIKE '%TEST_READ_VIOLATION%'
                   OR ""Name"" LIKE '%TEST_GRASS_VIOLATION%'
                   OR ""Name"" LIKE '%TEST_VIOLATION%'
                   OR ""Name"" LIKE '%TO_DELETE_VIOLATION%'
                   OR ""Name"" LIKE '%TO_DELETE_WITH_VIOLATIONS%'");
            
            // Re-enable foreign key checks
            await DbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT;");
        }
        catch (Exception ex)
        {
            // If cleanup fails, try a more aggressive approach
            try
            {
                // Re-enable foreign key checks in case the previous attempt failed
                await DbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT;");
                
                // Try to delete by ID ranges or other criteria
                await DbContext.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM ""Violations"" 
                    WHERE ""Description"" IS NOT NULL 
                      AND (""Description"" LIKE '%TEST%' OR ""Description"" LIKE '%Bulk%' OR ""Description"" LIKE '%Performance%' OR ""Description"" LIKE '%CompleteWorkflow%' OR ""Description"" LIKE '%BulkOperationsTest%')");
                
                await DbContext.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM ""ViolationTypes"" 
                    WHERE ""Name"" IS NOT NULL 
                      AND (""Name"" LIKE '%TEST%' OR ""Name"" LIKE '%TYPE%' OR ""Name"" LIKE '%PERF%')");
            }
            catch (Exception)
            {
                // If even the aggressive cleanup fails, we'll continue anyway
                // The test might still work with existing data
            }
        }
        finally
        {
            // Clear entity tracking after cleanup
            DbContext.ChangeTracker.Clear();
        }
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
} 
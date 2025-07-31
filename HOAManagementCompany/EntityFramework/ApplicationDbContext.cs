using HOAManagementCompany.Models;
using HOAManagementCompany.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    private readonly IServiceProvider? _serviceProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IServiceProvider? serviceProvider = null) 
        : base(options) 
    {
        _serviceProvider = serviceProvider;
    }
    
    public DbSet<Violation> Violations { get; set; }
    public DbSet<ViolationType> ViolationTypes { get; set; }
    
    // Test entities for audit functionality testing
    public DbSet<TestAuditableEntity> TestAuditableEntities { get; set; }
    public DbSet<TestNonAuditableEntity> TestNonAuditableEntities { get; set; }
    
    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditableEntity &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted));

        var currentUserId = GetCurrentUserId();

        foreach (var entry in entries)
        {
            var auditableEntity = (IAuditableEntity)entry.Entity;

            switch (entry.State)
            {
                case EntityState.Added:
                    auditableEntity.CreatedAt = DateTime.UtcNow;
                    auditableEntity.UpdatedAt = DateTime.UtcNow;
                    auditableEntity.CreatedBy = currentUserId;
                    auditableEntity.UpdatedBy = currentUserId;
                    break;

                case EntityState.Modified:
                    // Only update UpdatedAt and UpdatedBy for modified entities
                    auditableEntity.UpdatedAt = DateTime.UtcNow;
                    auditableEntity.UpdatedBy = currentUserId;

                    // Mark CreatedAt and CreatedBy as not modified to prevent EF from trying to update them
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Implement soft delete logic
                    entry.State = EntityState.Modified; // Change state to Modified
                    auditableEntity.IsDeleted = true;
                    auditableEntity.UpdatedAt = DateTime.UtcNow;
                    auditableEntity.UpdatedBy = currentUserId;

                    // Ensure other original properties are not marked as modified
                    foreach (var property in entry.OriginalValues.Properties)
                    {
                        var originalValue = entry.OriginalValues[property];
                        var currentValue = entry.CurrentValues[property];
                        if (!Equals(originalValue, currentValue) && 
                            property.Name != nameof(IAuditableEntity.IsDeleted) && 
                            property.Name != nameof(IAuditableEntity.UpdatedAt) && 
                            property.Name != nameof(IAuditableEntity.UpdatedBy))
                        {
                            entry.Property(property.Name).IsModified = false;
                        }
                    }
                    break;
            }
        }
    }

    private string? GetCurrentUserId()
    {
        // In a web application, you'd get the current authenticated user's ID
        // from HttpContext.User.Identity.Name or from claims.
        if (_serviceProvider != null)
        {
            var httpContextAccessor = _serviceProvider.GetService<IHttpContextAccessor>();
            if (httpContextAccessor?.HttpContext != null && httpContextAccessor.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
        }
        return null; // Default for background tasks or unauthenticated requests
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Violation to ViolationType relationship
        modelBuilder.Entity<Violation>()
            .HasOne(v => v.ViolationType)
            .WithMany()
            .HasForeignKey(v => v.ViolationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // Configure audit foreign key relationships for Violation
        modelBuilder.Entity<Violation>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(v => v.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<Violation>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(v => v.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
            
        // Configure audit foreign key relationships for ViolationType
        modelBuilder.Entity<ViolationType>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(vt => vt.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<ViolationType>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(vt => vt.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<ViolationType>().HasData(
            new ViolationType { Id = new Guid("b5a56c9b-a14f-4f9b-afc1-82d00663aa01"), Name = "GRASS", CovenantText = "Owners must maintain lawn (placeholder)..."},
        new ViolationType { Id = new Guid("3f843e9d-3e26-4696-84d6-f20f2dc20b1f"), Name = "POWERWASH", CovenantText = "Owners must maintain exterior (placeholder)..."}
            );
            
        // Configure test entities
        modelBuilder.Entity<TestAuditableEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
        
        modelBuilder.Entity<TestNonAuditableEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
        
        // Global query filters for soft delete functionality
        // These filters automatically exclude soft deleted records from all queries
        modelBuilder.Entity<Violation>().HasQueryFilter(v => !v.IsDeleted);
        modelBuilder.Entity<ViolationType>().HasQueryFilter(vt => !vt.IsDeleted);
        modelBuilder.Entity<TestAuditableEntity>().HasQueryFilter(te => !te.IsDeleted);

    }

    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Create roles if they don't exist
        var roles = new[] { HOAManagementCompany.Constants.Roles.Administrator, HOAManagementCompany.Constants.Roles.BoardMember, HOAManagementCompany.Constants.Roles.Homeowner };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
        
        // Remove any old "Admin" role if it exists
        if (await roleManager.RoleExistsAsync("Admin"))
        {
            var adminRole = await roleManager.FindByNameAsync("Admin");
            if (adminRole != null)
            {
                await roleManager.DeleteAsync(adminRole);
            }
        }

        // Create default admin user if it doesn't exist
        var adminUser = await userManager.FindByEmailAsync("admin@hoa.com");
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = "admin@hoa.com",
                Email = "admin@hoa.com",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, HOAManagementCompany.Constants.Roles.Administrator);
            }
        }
        else
        {
            // Ensure existing admin user has Administrator role
            var userRoles = await userManager.GetRolesAsync(adminUser);
            
            // Remove any "Admin" role and replace with "Administrator"
            if (userRoles.Contains("Admin"))
            {
                await userManager.RemoveFromRoleAsync(adminUser, "Admin");
            }
            
            if (!userRoles.Contains(HOAManagementCompany.Constants.Roles.Administrator))
            {
                await userManager.AddToRoleAsync(adminUser, HOAManagementCompany.Constants.Roles.Administrator);
            }
        }
    }
}
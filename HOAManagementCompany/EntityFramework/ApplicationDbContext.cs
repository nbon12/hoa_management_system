using HOAManagementCompany.Models;
using HOAManagementCompany.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<Violation> Violations { get; set; }
    public DbSet<ViolationType> ViolationTypes { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Violation to ViolationType relationship
        modelBuilder.Entity<Violation>()
            .HasOne(v => v.ViolationType)
            .WithMany()
            .HasForeignKey(v => v.ViolationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<ViolationType>().HasData(
            new ViolationType { Id = new Guid("b5a56c9b-a14f-4f9b-afc1-82d00663aa01"), Name = "GRASS", CovenantText = "Owners must maintain lawn (placeholder)..."},
        new ViolationType { Id = new Guid("3f843e9d-3e26-4696-84d6-f20f2dc20b1f"), Name = "POWERWASH", CovenantText = "Owners must maintain exterior (placeholder)..."}
            );
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

        // Seed role permissions
        await SeedRolePermissionsAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }
    }

    private static async Task SeedRolePermissionsAsync(ApplicationDbContext context)
    {
        // Check if permissions already exist
        if (await context.RolePermissions.AnyAsync())
            return;

        var permissions = new List<RolePermission>();

        // Administrator permissions - full access to everything
        var adminPermissions = new[]
        {
            Permissions.ViolationsRead, Permissions.ViolationsCreate, Permissions.ViolationsUpdate, Permissions.ViolationsDelete,
            Permissions.ViolationTypesRead, Permissions.ViolationTypesCreate, Permissions.ViolationTypesUpdate, Permissions.ViolationTypesDelete,
            Permissions.UsersRead, Permissions.UsersCreate, Permissions.UsersUpdate, Permissions.UsersDelete,
            Permissions.RolesManage
        };

        foreach (var permission in adminPermissions)
        {
            permissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = HOAManagementCompany.Constants.Roles.Administrator,
                Permission = permission,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Board Member permissions - read-only access to violations, read-only access to violation types
        var boardMemberPermissions = new[]
        {
            Permissions.ViolationsRead,
            Permissions.ViolationTypesRead
        };

        foreach (var permission in boardMemberPermissions)
        {
            permissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = HOAManagementCompany.Constants.Roles.BoardMember,
                Permission = permission,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Homeowner permissions - no access to violations or violation types
        // (No permissions added for homeowners)

        await context.RolePermissions.AddRangeAsync(permissions);
        await context.SaveChangesAsync();
    }
}
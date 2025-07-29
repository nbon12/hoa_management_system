using HOAManagementCompany.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<Violation> Violations { get; set; }
    public DbSet<ViolationType> ViolationTypes { get; set; }
    
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

        // Create admin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
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
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}
using HOAManagementCompany.Models;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<Violation> Violations { get; set; }
    public DbSet<ViolationType> ViolationTypes { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
}
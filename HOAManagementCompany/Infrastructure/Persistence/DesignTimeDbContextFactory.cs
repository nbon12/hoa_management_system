using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HOAManagementCompany.Infrastructure.Persistence;

// Design-time only: invoked by `dotnet ef migrations` / `database update`, never at
// runtime or under test, so it is intentionally excluded from coverage measurement.
[ExcludeFromCodeCoverage]
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Set the ConnectionStrings__DefaultConnection environment variable before running " +
                "EF Core design-time tools (dotnet ef migrations add / database update).");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}

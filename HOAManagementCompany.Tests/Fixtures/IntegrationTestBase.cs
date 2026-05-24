using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Fixtures;

[Collection("Integration")]
public abstract class IntegrationTestBase : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    protected readonly TestDatabaseFixture Fixture;
    protected readonly HttpClient Client;
    private IDbContextTransaction? _transaction;
    private readonly WebApplicationFactory<Program> _factory;

    protected IntegrationTestBase(TestDatabaseFixture fixture)
    {
        Fixture = fixture;

        // Set env vars before WebApplicationFactory starts so Program.cs reads them via builder.Configuration
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", fixture.ConnectionString);
        Environment.SetEnvironmentVariable("Storage__ServiceUrl", fixture.MinioEndpoint);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("environment", "Test");

                builder.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
                        ["Jwt:Secret"] = "test-secret-for-integration-tests-must-be-32-chars!!",
                        ["Jwt:Issuer"] = "nekohoa-api",
                        ["Jwt:Audience"] = "nekohoa-frontend",
                        ["Jwt:AccessTokenExpiryMinutes"] = "15",
                        ["Jwt:RefreshTokenExpiryDays"] = "30",
                        ["Storage:ServiceUrl"] = fixture.MinioEndpoint,
                        ["Storage:AccessKey"] = "minioadmin",
                        ["Storage:SecretKey"] = "minioadmin",
                        ["Storage:BucketName"] = "hoa-documents",
                        ["Storage:ForcePathStyle"] = "true",
                        ["Sentry:Dsn"] = ""
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Replace DbContext registrations with the Testcontainers connection
                    var toRemove = services
                        .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                                 || d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>))
                        .ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddDbContext<ApplicationDbContext>(o =>
                        o.UseNpgsql(fixture.ConnectionString));
                    services.AddDbContextFactory<ApplicationDbContext>(o =>
                        o.UseNpgsql(fixture.ConnectionString));
                });
            });

        Client = _factory.CreateClient();
    }

    protected async Task<IDbContextTransaction> BeginIsolatedAsync()
    {
        _transaction = await Fixture.DbContext.Database.BeginTransactionAsync();
        return _transaction;
    }

    protected async Task RollbackAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            _transaction = null;
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await RollbackAsync();
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<TestDatabaseFixture> { }

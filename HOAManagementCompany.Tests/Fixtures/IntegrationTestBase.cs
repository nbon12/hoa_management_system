using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor is not null) services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(o =>
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

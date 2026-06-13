using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Xunit;

namespace HOAManagementCompany.Tests.Fixtures;

public class TestDatabaseFixture : IAsyncLifetime
{
    // Randomised per test-session so no literal credentials appear in source.
    public string MinioAccessKey { get; } = Guid.NewGuid().ToString("N")[..20];
    public string MinioSecretKey { get; } = Guid.NewGuid().ToString("N");

    private readonly PostgreSqlContainer _postgres;
    private readonly MinioContainer _minio;

    public TestDatabaseFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("nekohoa_test")
            .WithUsername("nekohoa")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();

        _minio = new MinioBuilder()
            .WithUsername(MinioAccessKey)
            .WithPassword(MinioSecretKey)
            .Build();
    }

    public string ConnectionString => _postgres.GetConnectionString();
    public string MinioEndpoint => $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";
    public ApplicationDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        DbContext = new ApplicationDbContext(options);
        await DbContext.Database.MigrateAsync();

        // Seed test data (resident users, properties, community data)
        var seeder = new TestDataSeeder(DbContext);
        await seeder.SeedAsync();

        // Pre-create the MinIO bucket so document endpoints don't fail
        var s3 = new AmazonS3Client(
            new BasicAWSCredentials(MinioAccessKey, MinioSecretKey),
            new AmazonS3Config { ServiceURL = MinioEndpoint, ForcePathStyle = true });
        try
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = "hoa-documents", UseClientRegion = true });
        }
        catch { /* bucket may already exist */ }
    }

    public async Task DisposeAsync()
    {
        if (DbContext is not null)
            await DbContext.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _minio.DisposeAsync().AsTask());
    }
}

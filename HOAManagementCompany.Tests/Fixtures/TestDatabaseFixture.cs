using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
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
            // minio/minio is no longer pullable from Docker Hub (MinIO stopped publishing community
            // images), which breaks every Testcontainers run. pgsty/minio is a maintained, drop-in
            // community build of the same server; pinned for reproducibility.
            .WithImage("pgsty/minio:RELEASE.2026-08-04T00-00-00Z")
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

    /// <summary>
    /// 030: a new, empty database in the same Postgres container, for tests that must not share
    /// state with the main test database (the setup step reads the deployment's single company,
    /// and the migration test replays a legacy schema). Migrated to <paramref name="targetMigration"/>
    /// (or the latest) unless <paramref name="migrate"/> is false. Returns its connection string.
    /// </summary>
    public async Task<string> CreateFreshDatabaseAsync(bool migrate = true, string? targetMigration = null)
    {
        var name = "fresh_" + Guid.NewGuid().ToString("N");
        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
        if (migrate)
        {
            await using var ctx = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);
            await ctx.GetService<IMigrator>().MigrateAsync(targetMigration);
        }
        return connectionString;
    }

    public async Task DisposeAsync()
    {
        if (DbContext is not null)
            await DbContext.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _minio.DisposeAsync().AsTask());
    }
}

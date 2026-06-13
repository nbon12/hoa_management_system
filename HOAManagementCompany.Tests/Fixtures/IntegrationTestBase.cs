using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Minio;
using Minio.DataModel.Args;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog.Core;
using Xunit;
using Metric = OpenTelemetry.Metrics.Metric;

namespace HOAManagementCompany.Tests.Fixtures;

[Collection("Integration")]
public abstract class IntegrationTestBase : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    protected readonly TestDatabaseFixture Fixture;
    protected readonly HttpClient Client;
    protected IServiceProvider Services => _factory.Services;
    private IDbContextTransaction? _transaction;
    private readonly WebApplicationFactory<Program> _factory;
    private static bool _testDocumentsUploaded;

    // One traced Npgsql data source per connection string, shared across all factories.
    private static readonly ConcurrentDictionary<string, NpgsqlDataSource> SharedTracedDataSources = new();

    // ── Telemetry capture (no external telemetry service — FR-024/FR-025) ──────
    private readonly List<Activity> _exportedSpans = new();
    private readonly List<Metric> _exportedMetrics = new();
    private readonly InMemoryLogSink _logSink = new();

    /// <summary>Server spans exported to the in-memory trace exporter (synchronous on end).</summary>
    protected IReadOnlyList<Activity> ExportedSpans => _exportedSpans;

    /// <summary>Metrics collected by the in-memory metric reader (call <see cref="FlushTelemetry"/> first).</summary>
    protected IReadOnlyList<Metric> ExportedMetrics => _exportedMetrics;

    /// <summary>Structured log records captured by the real in-memory Serilog sink.</summary>
    protected InMemoryLogSink LogSink => _logSink;

    /// <summary>Forces the metric reader (and tracer) to flush so assertions are deterministic.</summary>
    protected void FlushTelemetry(int timeoutMilliseconds = 5000)
    {
        Services.GetService<TracerProvider>()?.ForceFlush(timeoutMilliseconds);
        Services.GetService<MeterProvider>()?.ForceFlush(timeoutMilliseconds);
    }

    /// <summary>
    /// Override to supply per-test configuration that wins over the harness defaults.
    /// Implementations must be stateless (this runs from the base constructor).
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        Array.Empty<KeyValuePair<string, string?>>();

    /// <summary>
    /// Override to register or replace services for a test (runs from the base
    /// constructor, after the harness's own service overrides).
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

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
                        // Allow Information-level logs so observability tests can read the
                        // structured request log from the in-memory Serilog sink.
                        ["Serilog:MinimumLevel:Default"] = "Information",
                        // Small telemetry-proxy rate limit so the 429 test is fast/deterministic.
                        ["Observability:TelemetryProxyRateLimitPerMinute"] = "5",
                        // Jwt:Secret is intentionally NOT overridden here. Program.cs reads it at
                        // startup (for token validation) BEFORE WebApplicationFactory's in-memory
                        // config is applied, while AuthService reads it at request time (for signing)
                        // AFTER. Overriding it here would only change the signing side, so tokens
                        // would be signed with one key and validated with another → 401. Letting both
                        // fall back to appsettings.Test.json keeps them in agreement. That file is
                        // listed in sonar.exclusions, so the test-only secret triggers no S2068.
                        ["Jwt:Issuer"] = "nekohoa-api",
                        ["Jwt:Audience"] = "nekohoa-frontend",
                        ["Jwt:AccessTokenExpiryMinutes"] = "15",
                        ["Jwt:RefreshTokenExpiryDays"] = "30",
                        ["Storage:ServiceUrl"] = fixture.MinioEndpoint,
                        ["Storage:AccessKey"] = fixture.MinioAccessKey,
                        ["Storage:SecretKey"] = fixture.MinioSecretKey,
                        ["Storage:BucketName"] = "hoa-documents",
                        ["Storage:ForcePathStyle"] = "true",
                        ["Sentry:Dsn"] = "",
                        // Non-functional placeholders so strict startup validation (008) passes.
                        // Tests use FakeStripeGateway, so these are never sent to Stripe.
                        ["Stripe:SecretKey"] = "sk_test_placeholder",
                        ["Stripe:PublishableKey"] = "pk_test_placeholder",
                        ["Stripe:WebhookSigningSecret"] = "whsec_placeholder",
                        ["Jobs:SchedulerSharedSecret"] = "test-scheduler-shared-secret-placeholder"
                    });

                    // Per-test overrides (e.g. CaptureSqlText) win over the defaults above.
                    var overrides = ExtraConfiguration().ToList();
                    if (overrides.Count > 0)
                        cfg.AddInMemoryCollection(overrides);
                });

                builder.ConfigureServices(services =>
                {
                    // Replace DbContext registrations with the Testcontainers connection.
                    // Use the traced data source so DB spans carry SQL text (FR-004).
                    var toRemove = services
                        .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                                 || d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>)
                                 || d.ServiceType == typeof(NpgsqlDataSource))
                        .ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    // Reuse ONE traced data source per connection string across all test
                    // factories. A distinct instance per factory makes EF Core build a new
                    // internal service provider each time (ManyServiceProvidersCreatedWarning);
                    // a shared instance keeps EF's provider cache warm and avoids pool churn.
                    var tracedDataSource = SharedTracedDataSources.GetOrAdd(
                        fixture.ConnectionString, ObservabilityNpgsql.BuildTracedDataSource);
                    services.AddSingleton(tracedDataSource);
                    // Each test builds a fresh WebApplicationFactory (its own root provider), so
                    // EF spins up a new internal provider per factory — benign in tests but it
                    // trips ManyServiceProvidersCreatedWarning after 20. Suppress it here only.
                    services.AddDbContext<ApplicationDbContext>(o => o
                        .UseNpgsql(tracedDataSource)
                        .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                    services.AddDbContextFactory<ApplicationDbContext>(o => o
                        .UseNpgsql(tracedDataSource)
                        .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

                    // Replace S3 client with one configured for Testcontainers MinIO
                    var s3Descriptors = services.Where(d => d.ServiceType == typeof(IAmazonS3)).ToList();
                    foreach (var d in s3Descriptors) services.Remove(d);
                    services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
                        new BasicAWSCredentials(fixture.MinioAccessKey, fixture.MinioSecretKey),
                        new AmazonS3Config
                        {
                            ServiceURL = fixture.MinioEndpoint,
                            ForcePathStyle = true,
                            AuthenticationRegion = "us-east-1",
                            UseHttp = fixture.MinioEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                        }));

                    // Append in-memory OTel exporters to the app's tracer/meter providers so
                    // tests can read spans and metrics without any external collector.
                    services.ConfigureOpenTelemetryTracerProvider(tracing =>
                        tracing.AddInMemoryExporter(_exportedSpans));
                    services.ConfigureOpenTelemetryMeterProvider(metrics =>
                        metrics.AddInMemoryExporter(_exportedMetrics));

                    // Compose a real Serilog sink via ReadFrom.Services for log assertions.
                    services.AddSingleton<ILogEventSink>(_logSink);

                    // Per-test service registrations/replacements.
                    ConfigureTestServices(services);
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

    /// <summary>Uploads placeholder PDFs to Testcontainers MinIO for document download tests.</summary>
    protected async Task EnsureTestDocumentsInStorageAsync()
    {
        if (_testDocumentsUploaded) return;

        var endpointUri = new Uri(Fixture.MinioEndpoint);
        var minio = new MinioClient()
            .WithEndpoint($"{endpointUri.Host}:{endpointUri.Port}")
            .WithCredentials(Fixture.MinioAccessKey, Fixture.MinioSecretKey)
            .WithSSL(endpointUri.Scheme == "https")
            .Build();

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4\n%Placeholder for integration tests\n%%EOF");

        foreach (var key in new[]
        {
            "documents/2026/budget.pdf",
            "documents/rules/rules.pdf",
            "documents/governing/ccr-declaration.pdf",
        })
        {
            await using var stream = new MemoryStream(pdfBytes);
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket("hoa-documents")
                .WithObject(key)
                .WithStreamData(stream)
                .WithObjectSize(pdfBytes.Length)
                .WithContentType("application/pdf"));
        }

        _testDocumentsUploaded = true;
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

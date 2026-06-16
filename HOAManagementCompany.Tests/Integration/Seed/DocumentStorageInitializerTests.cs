using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Seed;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Seed;

/// <summary>
/// Covers <see cref="DocumentStorageInitializer.EnsureValidPdfsAsync"/> against the fixture's MinIO,
/// where the <c>hoa-documents</c> bucket already exists — so <c>PutBucket</c> throws an
/// <see cref="AmazonS3Exception"/> (BucketAlreadyOwnedByYou). The initializer must treat that as
/// non-fatal and continue, exactly as it now must for Cloudflare R2 (where the runtime token returns
/// 403 Access Denied for bucket creation). Regression for the live Dev deploy crash.
/// </summary>
[Collection("Integration")]
public class DocumentStorageInitializerTests(TestDatabaseFixture fixture)
{
    [Fact]
    public async Task EnsureValidPdfs_PreexistingBucket_DoesNotThrow()
    {
        var opts = Options.Create(new StorageOptions
        {
            ServiceUrl = fixture.MinioEndpoint,
            AccessKey = fixture.MinioAccessKey,
            SecretKey = fixture.MinioSecretKey,
            BucketName = "hoa-documents", // pre-created by the fixture
            ForcePathStyle = true,
        });

        using var s3 = new AmazonS3Client(
            new BasicAWSCredentials(fixture.MinioAccessKey, fixture.MinioSecretKey),
            new AmazonS3Config { ServiceURL = fixture.MinioEndpoint, ForcePathStyle = true });

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);

        var storage = new S3DocumentStorage(s3, opts);
        var initializer = new DocumentStorageInitializer(
            db, storage, s3, opts, NullLogger<DocumentStorageInitializer>.Instance);

        // PutBucket on the existing bucket throws AmazonS3Exception; the initializer must swallow it
        // and complete (the previous code only caught "already exists" and crashed on R2's 403).
        var ex = await Record.ExceptionAsync(() => initializer.EnsureValidPdfsAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task EnsureValidPdfs_WhenUploadsFail_ReportsErrorAndDoesNotClaimSuccess()
    {
        var opts = Options.Create(new StorageOptions
        {
            ServiceUrl = fixture.MinioEndpoint,
            AccessKey = fixture.MinioAccessKey,
            SecretKey = fixture.MinioSecretKey,
            BucketName = "hoa-documents",
            ForcePathStyle = true,
        });

        using var s3 = new AmazonS3Client(
            new BasicAWSCredentials(fixture.MinioAccessKey, fixture.MinioSecretKey),
            new AmazonS3Config { ServiceURL = fixture.MinioEndpoint, ForcePathStyle = true });

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);

        var logger = new CapturingLogger<DocumentStorageInitializer>();
        var initializer = new DocumentStorageInitializer(
            db, new ThrowingDocumentStorage(), s3, opts, logger);

        // Mirrors the live Dev failure: R2 rejected every upload (streaming-trailer 501). Each failure
        // must be swallowed per-doc (the run still completes), but it must be reported at Error and must
        // NOT log the "Refreshed" success line — that misreport is what hid the breakage behind a
        // healthy-looking startup.
        var ex = await Record.ExceptionAsync(() => initializer.EnsureValidPdfsAsync());

        Assert.Null(ex);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Refreshed"));
    }

    /// <summary>Fails every upload, simulating R2 rejecting the request (e.g. the streaming-trailer 501).</summary>
    private sealed class ThrowingDocumentStorage : IDocumentStorage
    {
        public Task<string> GetPreSignedUrlAsync(string storageKey, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UploadAsync(string storageKey, byte[] content, string contentType = "application/pdf", CancellationToken ct = default)
            => throw new AmazonS3Exception("STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER not implemented");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}

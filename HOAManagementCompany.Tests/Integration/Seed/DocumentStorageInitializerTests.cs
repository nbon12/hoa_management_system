using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Seed;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
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
}

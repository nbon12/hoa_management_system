using Amazon.S3;
using Amazon.S3.Model;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Seed;

/// <summary>
/// Ensures the MinIO bucket exists and every seeded document key contains a valid PDF.
/// Runs on Development startup only.
/// </summary>
public class DocumentStorageInitializer(
    ApplicationDbContext db,
    IDocumentStorage storage,
    IAmazonS3 s3,
    IOptions<StorageOptions> storageOpts,
    ILogger<DocumentStorageInitializer> logger)
{
    public async Task EnsureValidPdfsAsync(CancellationToken ct = default)
    {
        var bucket = storageOpts.Value.BucketName;
        try
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket, UseClientRegion = true }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            // Creating the bucket is a local-MinIO convenience. In deployed environments the bucket is
            // provisioned by IaC and the runtime credentials intentionally CANNOT create buckets
            // (e.g. a Cloudflare R2 "Object Read & Write" token returns 403 Access Denied for
            // PutBucket). Treat any create failure as non-fatal — the uploads below will surface a
            // genuinely missing or inaccessible bucket.
            logger.LogDebug(ex, "Skipping bucket create for {Bucket} (already exists or externally managed).", bucket);
        }

        var docs = await db.HoaDocuments.AsNoTracking().Select(d => new { d.StorageKey }).ToListAsync(ct);
        if (docs.Count == 0)
        {
            logger.LogDebug("No documents in database — skipping storage refresh.");
            return;
        }

        foreach (var doc in docs)
        {
            try
            {
                var pdfBytes = await TestDataFiles.ReadSamplePdfAsync(ct);
                await storage.UploadAsync(doc.StorageKey, pdfBytes, ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not refresh PDF for {StorageKey}", doc.StorageKey);
            }
        }

        logger.LogInformation("Refreshed {Count} document PDFs in object storage.", docs.Count);
    }
}

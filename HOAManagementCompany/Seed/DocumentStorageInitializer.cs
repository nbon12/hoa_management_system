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
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // ok
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

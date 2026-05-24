using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Infrastructure.Storage;

public class S3DocumentStorage(IAmazonS3 s3Client, IOptions<StorageOptions> opts) : IDocumentStorage
{
    private readonly StorageOptions _opts = opts.Value;

    public Task<string> GetPreSignedUrlAsync(string storageKey, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key = storageKey,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Verb = HttpVerb.GET
        };
        return Task.FromResult(s3Client.GetPreSignedURL(request));
    }

    public async Task UploadAsync(string storageKey, string content, string contentType = "application/pdf", CancellationToken ct = default)
    {
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = storageKey,
            ContentBody = content,
            ContentType = contentType
        }, ct);
    }
}

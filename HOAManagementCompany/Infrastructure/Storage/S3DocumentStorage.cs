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
            Verb = HttpVerb.GET,
        };
        var url = s3Client.GetPreSignedURL(request);
        if (_opts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            url = url.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(_opts.PublicServiceUrl))
        {
            var internalAuthority = new Uri(_opts.ServiceUrl).Authority;
            var publicAuthority = new Uri(_opts.PublicServiceUrl).Authority;
            url = url.Replace(internalAuthority, publicAuthority, StringComparison.OrdinalIgnoreCase);
        }

        return Task.FromResult(url);
    }

    public async Task UploadAsync(string storageKey, byte[] content, string contentType = "application/pdf", CancellationToken ct = default)
    {
        await using var stream = new MemoryStream(content);
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = storageKey,
            InputStream = stream,
            ContentType = contentType,
            Headers = { ContentLength = content.Length },
            // Cloudflare R2 implements neither the SDK's chunked streaming signature
            // ("STREAMING-AWS4-HMAC-SHA256-PAYLOAD not implemented" → 501) nor its checksum-trailer
            // variant. DisablePayloadSigning sends a single UNSIGNED-PAYLOAD request instead; integrity
            // is still covered by TLS. UseChunkEncoding=false makes the non-chunked intent explicit.
            // Verified against live R2: without these the upload 501s; with them it succeeds. MinIO
            // accepts both forms, so local/Test behaviour is unchanged.
            DisablePayloadSigning = true,
            UseChunkEncoding = false,
        }, ct);
    }
}

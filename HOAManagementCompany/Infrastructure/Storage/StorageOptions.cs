namespace HOAManagementCompany.Infrastructure.Storage;

public class StorageOptions
{
    /// <summary>Internal endpoint used by the API to talk to MinIO/S3 (may be a Docker hostname).</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>Browser-reachable endpoint substituted into presigned download URLs (e.g. http://localhost:9000).</summary>
    public string? PublicServiceUrl { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "hoa-documents";
    public bool ForcePathStyle { get; set; }
}

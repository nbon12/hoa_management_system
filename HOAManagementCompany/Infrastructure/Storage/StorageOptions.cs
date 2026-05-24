namespace HOAManagementCompany.Infrastructure.Storage;

public class StorageOptions
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "hoa-documents";
    public bool ForcePathStyle { get; set; }
}

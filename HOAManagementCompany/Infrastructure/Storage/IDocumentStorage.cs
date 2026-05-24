namespace HOAManagementCompany.Infrastructure.Storage;

public interface IDocumentStorage
{
    Task<string> GetPreSignedUrlAsync(string storageKey, CancellationToken ct = default);
    Task UploadAsync(string storageKey, string content, string contentType = "application/pdf", CancellationToken ct = default);
}

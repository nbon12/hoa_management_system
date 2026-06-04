namespace HOAManagementCompany.Infrastructure.Storage;

public interface IDocumentStorage
{
    Task<string> GetPreSignedUrlAsync(string storageKey, CancellationToken ct = default);
    Task UploadAsync(string storageKey, byte[] content, string contentType = "application/pdf", CancellationToken ct = default);
}

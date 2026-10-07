namespace HOAManagementCompany.Infrastructure.Storage;

public interface IDocumentStorage
{
    Task<string> GetPreSignedUrlAsync(string storageKey, CancellationToken ct = default);
    /// <summary>True when an object exists at <paramref name="storageKey"/> (027: attachment unavailable state).</summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
    Task UploadAsync(string storageKey, byte[] content, string contentType = "application/pdf", CancellationToken ct = default);
}

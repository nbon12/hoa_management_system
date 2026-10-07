namespace HOAManagementCompany.Domain.Entities;

// A file supporting one application revision. The object lives in private storage
// (R2/MinIO); only metadata and the key are stored here (027 FR-012). A key may be
// shared by several revisions when attachments are carried over.
public class ArchitecturalAttachment
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ArchitecturalApplication Application { get; set; } = null!;
}

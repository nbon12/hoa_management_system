namespace HOAManagementCompany.Domain.Entities;

// A file uploaded to a resident draft (029). The object lives in private storage under
// arc/{communityId}/drafts/{draftId}/{guid}; on submit the application's attachment row reuses the
// same key, so the object is never copied. ContentType is the sniffed type, never the client's.
public class ArchitecturalDraftAttachment
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ArchitecturalApplicationDraft Draft { get; set; } = null!;
}

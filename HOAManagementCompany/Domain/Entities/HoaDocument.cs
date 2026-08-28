using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class HoaDocument
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string FileSizeLabel { get; set; } = string.Empty;
    public bool Pinned { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

namespace HOAManagementCompany.Domain.Entities;

// A homeowner's comment on their own application (031), e.g. a reply to a revision request, with
// optional files (ArchitecturalAttachment.OwnerCommentId). Visible to the owner, the community's
// board members and managers. Immutable once posted.
public class ArchitecturalOwnerComment
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ArchitecturalApplication Application { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
}

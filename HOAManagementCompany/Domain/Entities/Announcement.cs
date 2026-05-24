using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

public class Announcement
{
    public Guid Id { get; set; }
    public string CommunityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public AnnouncementCategory Category { get; set; }
    public bool Pinned { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRole { get; set; }
    public string? ImageUrl { get; set; }
}

namespace HOAManagementCompany.Features.Community.Models;

public record AnnouncementListRequest(int Page = 1, int PageSize = 50, string? Category = null, bool? Pinned = null);

public record AnnouncementListResponse(IEnumerable<AnnouncementDto> Items, int TotalCount, int Page, int PageSize);

public record AnnouncementDto(Guid Id, string Title, string Body, string Category, DateTimeOffset PublishedAt, bool Pinned, int LikeCount, int CommentCount, string AuthorName, string? AuthorRole, string? ImageUrl);

public record PollDto(Guid Id, string Question, string ClosingLabel, int TotalVotes, IEnumerable<PollOptionDto> Options);

public record PollOptionDto(int OptionIndex, string OptionText, int VoteCount, decimal Percentage);

public record PollVoteRequest(int OptionIndex);

public record ViolationListRequest(int Page = 1, int PageSize = 50, string? Status = null, string? Category = null);

public record ViolationListResponse(IEnumerable<ViolationDto> Items, int TotalCount, int Page, int PageSize);

public record ViolationDto(Guid Id, string Title, string? Description, string Category, string Status, DateOnly IssuedDate, DateOnly? ResolvedDate, DateOnly? DueDate, decimal? FineAmount, string? ImageUrl);

public record EventListRequest(int Page = 1, int PageSize = 50, string? StartDate = null, string? EndDate = null, string? Category = null);

public record EventListResponse(IEnumerable<EventDto> Items, int TotalCount, int Page, int PageSize);

public record EventDto(Guid Id, string Title, string? Description, DateTimeOffset EventDate, string? Location, string Category, bool RsvpEnabled, int RsvpCount);

public record EventRsvpRequest(bool Attending);

public record DocumentListRequest(int Page = 1, int PageSize = 50, string? Category = null, string? Search = null);

public record DocumentListResponse(IEnumerable<DocumentDto> Items, int TotalCount, int Page, int PageSize);

public record DocumentDto(Guid Id, string Name, string Category, DateOnly EffectiveDate, string FileSizeLabel, bool Pinned);

public record DocumentDownloadResponse(string Url, DateTimeOffset ExpiresAt);

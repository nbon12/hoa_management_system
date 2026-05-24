namespace HOAManagementCompany.Features.Dashboard.Models;

public record DashboardResponse(
    decimal CurrentBalance,
    string BalanceDueDate,
    int OpenViolations,
    int DocumentCount,
    int NewDocumentsThisMonth,
    AnnouncementSummary? PinnedAnnouncement,
    IEnumerable<EventSummary> ThisWeekEvents,
    EventSummary? NextEvent,
    IEnumerable<LedgerSummary> RecentActivity,
    IEnumerable<ExpenseSummary> CommunityExpenses);

public record AnnouncementSummary(Guid Id, string Title, string Body, string Category, DateTimeOffset PublishedAt, string AuthorName);

public record EventSummary(Guid Id, string Title, DateTimeOffset EventDate, string? Location, string Category, bool RsvpEnabled);

public record LedgerSummary(Guid Id, DateOnly EntryDate, string Description, decimal ChargeAmount, decimal PaymentAmount, decimal RunningBalance, string EntryType);

public record ExpenseSummary(Guid Id, string Label, string Color, decimal Amount);

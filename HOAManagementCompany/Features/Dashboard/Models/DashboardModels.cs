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
    IEnumerable<ExpenseSummary> CommunityExpenses,
    ArchitecturalInfoRequestedSummary ArchitecturalInfoRequested);

/// <summary>
/// 029 FR-017 dashboard alert: how many of the active property's open architectural requests have an
/// unanswered board question, and the oldest one to link to (null when none).
/// </summary>
public record ArchitecturalInfoRequestedSummary(int Count, Guid? ApplicationId);

public record AnnouncementSummary(Guid Id, string Title, string Body, string Category, DateTimeOffset PublishedAt, string AuthorName);

public record EventSummary(Guid Id, string Title, DateTimeOffset EventDate, string? Location, string Category, bool RsvpEnabled);

public record LedgerSummary(Guid Id, DateOnly EntryDate, string Description, decimal ChargeAmount, decimal PaymentAmount, decimal RunningBalance, string EntryType);

public record ExpenseSummary(Guid Id, string Label, string Color, decimal Amount);

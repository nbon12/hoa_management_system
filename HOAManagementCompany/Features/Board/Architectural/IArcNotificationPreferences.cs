namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// Seam for the sibling Notification Settings spec (027 research R5): decides whether an optional
/// board email (reminder, lapse notice) goes to a user. Until that spec lands, everyone receives them.
/// Owner outcome emails are mandatory and never pass through this check.
/// </summary>
public interface IArcNotificationPreferences
{
    Task<bool> WantsBoardEmailAsync(string userId, Guid communityId, string kind, CancellationToken ct);
}

public sealed class AllowAllArcNotificationPreferences : IArcNotificationPreferences
{
    public Task<bool> WantsBoardEmailAsync(string userId, Guid communityId, string kind, CancellationToken ct) =>
        Task.FromResult(true);
}

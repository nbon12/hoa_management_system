using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Board;

// Standard limit/offset paged envelope (constitution §4/§5). Default limit 25, max 100.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);

public static class Paging
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    public static (int limit, int offset) Normalize(int? limit, int? offset)
    {
        var l = limit is null or < 1 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
        var o = offset is null or < 0 ? 0 : offset.Value;
        return (l, o);
    }
}

// GET /me/communities
public sealed record MyCommunitiesQuery
{
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}

public sealed record CommunitySummaryDto(Guid Id, string CommunityName, string Role, string Status);

// GET /communities/{communityId}/memberships
public sealed record MembershipsListQuery
{
    public Guid CommunityId { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}

public sealed record MembershipDto(
    Guid Id, string UserId, string UserDisplayName, string Role, string Status,
    DateOnly StartDate, DateOnly? EndDate);

// POST /communities/{communityId}/memberships
public sealed record MembershipCreateRequest
{
    public Guid CommunityId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public CommunityRole Role { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

// PATCH /communities/{communityId}/memberships/{membershipId}
public sealed record MembershipUpdateRequest
{
    public Guid CommunityId { get; init; }
    public Guid MembershipId { get; init; }
    public CommunityRole? Role { get; init; }
    public MembershipStatus? Status { get; init; }
    public DateOnly? EndDate { get; init; }
}

// GET /board/metrics
public sealed record BoardMetricsQuery
{
    public Guid CommunityId { get; init; }
    public MetricSurface Surface { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}

public sealed record MetricRowDto(
    string Id, string Label, string DefinitionText, string Value, string? Detail,
    string Status, string Emphasis);

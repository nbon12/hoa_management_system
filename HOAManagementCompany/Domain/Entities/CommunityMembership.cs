using System.Linq.Expressions;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// CommunityMembership: the sole source of board-side authorization (spec FR-007,
// FR-012). Independent of property ownership (FR-011) — deliberately separate
// from UserProperty. A user MAY hold more than one role in a community; the
// resolver grants the union of their capabilities (Clarifications, 2026-08-23).
// <!-- REPOWISE:END -->

public class CommunityMembership
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid CommunityId { get; set; }
    public CommunityRole Role { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public Community Community { get; set; } = null!;

    /// <summary>
    /// The single definition of the effective-permission rule (data-model.md): a membership
    /// confers permission only while it is Active, its term has not ended (open-ended, or
    /// EndDate on/after <paramref name="today"/> UTC), and its community is itself Active —
    /// an inactive/offboarded community confers no access to anyone (spec Edge Cases).
    /// Returned as an <see cref="Expression"/> so EF Core translates it into SQL; callers
    /// AND their own conditions on with additional <c>Where</c> clauses.
    /// </summary>
    public static Expression<Func<CommunityMembership, bool>> IsEffectiveAsOf(DateOnly today) =>
        m => m.Status == MembershipStatus.Active
          && (m.EndDate == null || m.EndDate >= today)
          && m.Community.Status == CommunityStatus.Active;
}

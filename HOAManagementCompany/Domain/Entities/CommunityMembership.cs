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
}

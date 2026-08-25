using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// Community: the tenant boundary for all board-side features (spec FR-001).
// Stable GUID identity; CommunityName is the unique human-readable handle.
// Self-referencing ParentCommunityId models master/sub-association (FR-003).
// <!-- REPOWISE:END -->

public class Community
{
    public Guid Id { get; set; }

    // Backfilled empty at migration time; filled in later by a community manager.
    public string LegalName { get; set; } = string.Empty;

    // The management company's human-readable handle. Unique (research.md R1).
    public string CommunityName { get; set; } = string.Empty;

    public string? County { get; set; }
    public DateOnly? FormationDate { get; set; }
    public DateOnly? ManagementStartDate { get; set; }
    public string? Description { get; set; }
    public CommunityStatus Status { get; set; } = CommunityStatus.Active;

    // Null for a master association or a standalone community; set for a
    // sub-association. Scope is decided strictly per row — the resolver never
    // traverses this relation (spec Clarifications, 2026-08-23).
    public Guid? ParentCommunityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Community? ParentCommunity { get; set; }
    public ICollection<Community> SubCommunities { get; set; } = [];
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<CommunityMembership> Memberships { get; set; } = [];
}

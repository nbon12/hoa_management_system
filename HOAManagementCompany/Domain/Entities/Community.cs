using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// Community: the tenant boundary for all board-side features (025 FR-001). It belongs to one
// management company (030 FR-001). Stable GUID identity; CommunityName is the human-readable
// handle, unique per company without regard to case or surrounding spaces (030 FR-007, via
// NormalizedName). Self-referencing ParentCommunityId models master/sub-association (FR-003).
// Lifecycle (030 FR-010): Onboarding → Active → Archived. A restore returns to Active only if the
// community was ever activated (ActivatedAt set) and has an open-ended manager; otherwise to
// Onboarding.
// <!-- REPOWISE:END -->

public class Community
{
    private string _communityName = string.Empty;

    public Guid Id { get; set; }

    public Guid ManagementCompanyId { get; set; }

    // Backfilled empty at migration time; filled in later by a community manager.
    public string LegalName { get; set; } = string.Empty;

    // The management company's human-readable handle. Setting it also sets NormalizedName, so
    // the two can never drift (030 R15).
    public string CommunityName
    {
        get => _communityName;
        set
        {
            _communityName = value;
            NormalizedName = Normalize(value);
        }
    }

    // lower(trim(CommunityName)); unique with ManagementCompanyId (030 FR-007, R15).
    public string NormalizedName { get; private set; } = string.Empty;

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
    public string? CreatedByUserId { get; set; }

    // Lifecycle stamps (030 R4). ActivatedAt also records that the community was ever live.
    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedByUserId { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedByUserId { get; set; }
    public DateTimeOffset? RestoredAt { get; set; }
    public string? RestoredByUserId { get; set; }

    public ManagementCompany ManagementCompany { get; set; } = null!;
    public Community? ParentCommunity { get; set; }
    public ICollection<Community> SubCommunities { get; set; } = [];
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<CommunityMembership> Memberships { get; set; } = [];

    public static string Normalize(string name) => name.Trim().ToLowerInvariant();
}

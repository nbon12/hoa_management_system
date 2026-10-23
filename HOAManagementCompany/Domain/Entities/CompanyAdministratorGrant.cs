using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// CompanyAdministratorGrant: the company-level Company Administrator role (030 FR-002), separate
// from community memberships. A grant is live until revoked; the table keeps the history and the
// audit trail stays in Serilog. Revoking the last live grant is refused (FR-037).
// <!-- REPOWISE:END -->

public class CompanyAdministratorGrant
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public CompanyGrantSource Source { get; set; }
    public string? GrantedByUserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? InvitationId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }

    public bool IsLive => RevokedAt == null;

    public ManagementCompany Company { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

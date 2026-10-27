namespace HOAManagementCompany.Domain.Entities;

// <!-- REPOWISE:START domain=entities -->
// ManagementCompany: the HOA management company running this deployment (030 FR-001). It owns
// its communities, its Company Administrators and its default settings. There is one per
// deployment for now, but every query filters by its id, so nothing assumes only one.
// <!-- REPOWISE:END -->

public class ManagementCompany
{
    public const string DefaultName = "Management company";

    public Guid Id { get; set; }
    public string Name { get; set; } = DefaultName;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public CompanyDefaultSettings? DefaultSettings { get; set; }
    public ICollection<Community> Communities { get; set; } = [];
    public ICollection<CompanyAdministratorGrant> AdministratorGrants { get; set; } = [];
}

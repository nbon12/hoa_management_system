using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

// The deployment's management company (030 FR-001, R1): the row the AddCompanyAdministration
// migration created, which is the oldest. Only the setup step and the seeders ask for it; every
// request resolves its company from the caller or the community instead (ICompanyContext).
public static class DeploymentCompany
{
    public static Task<Guid> IdAsync(ApplicationDbContext db, CancellationToken ct = default) =>
        db.ManagementCompanies
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => c.Id)
            .FirstAsync(ct);
}

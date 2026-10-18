using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Infrastructure.Persistence;

namespace HOAManagementCompany.Features.Company;

// 030 R5: serializes Company Administrator revokes (and the setup step) per company, so the last
// live grant can't be revoked by two concurrent requests (US7-S4).
public static class CompanyLocks
{
    internal const int KeySpace = 30_002;

    public static Task<T> RunAsync<T>(ApplicationDbContext db, Guid companyId, Func<Task<T>> body, CancellationToken ct) =>
        AdvisoryLock.RunAsync(db, KeySpace, companyId, body, ct);
}

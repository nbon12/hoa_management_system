using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

// Row lock for decision-changing writes (027 research R4): inside a transaction, lock the
// application row, then reload it so the caller sees the committed state of competing writers.
internal static class ArcLocks
{
    public static async Task LockAndReloadAsync(ApplicationDbContext db, ArchitecturalApplication app, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "ArchitecturalApplications" WHERE "Id" = {app.Id} FOR UPDATE""", ct);
        await db.Entry(app).ReloadAsync(ct);
    }
}

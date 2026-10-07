using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>A refusal raised inside a locked unit of work, written to the response afterwards.</summary>
internal sealed record ArcRefusal(int Status, string Code, string Message);

// Row lock for decision-changing writes (027 research R4). Runs the unit inside the retrying
// execution strategy (EnableRetryOnFailure forbids a bare user transaction, as in payments), opens
// a transaction, locks the application row and reloads it so the body sees the committed state of
// competing writers. The body returns a refusal (rolled back) or null (committed).
internal static class ArcLocks
{
    public static Task<ArcRefusal?> InLockedTransactionAsync(
        ApplicationDbContext db, ArchitecturalApplication app, Func<Task<ArcRefusal?>> body, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM "ArchitecturalApplications" WHERE "Id" = {app.Id} FOR UPDATE""", ct);
            await db.Entry(app).ReloadAsync(ct);

            var refusal = await body();
            if (refusal is null)
                await tx.CommitAsync(ct);
            return refusal;
        });
    }

    public static Task WriteAsync(HttpContext ctx, ArcRefusal r, CancellationToken ct) =>
        ArcHttp.ErrorAsync(ctx, r.Status, r.Code, r.Message, ct);
}

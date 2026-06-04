using FastEndpoints;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.DevTools;

/// <summary>
/// Dev-only endpoint: deletes E2E test users (email LIKE 'e2e+%@test.dev')
/// so that the registration test can run against an unclaimed property each time.
/// Rejected with 404 outside the Development environment.
/// </summary>
public class E2ECleanupEndpoint(ApplicationDbContext db, IWebHostEnvironment env)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/e2e/cleanup");
        AllowAnonymous();
        Description(x => x.WithTags("DevTools").ExcludeFromDescription());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment())
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var deleted = await db.Users
            .Where(u => u.Email != null && EF.Functions.Like(u.Email, "e2e+%@test.dev"))
            .ExecuteDeleteAsync(ct);

        await SendAsync(new { deleted }, 200, ct);
    }
}

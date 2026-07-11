using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.DevTools;

// <!-- REPOWISE:START domain=devtools -->
// DELETE /e2e/cleanup — removes e2e+*@test.dev users; 404 unless DevTools:E2ECleanupEnabled.
// <!-- REPOWISE:END -->

/// <summary>
/// Test-support endpoint: deletes E2E test users (email LIKE 'e2e+%@test.dev')
/// so that the registration test can run against an unclaimed property each time.
/// Gated on the <c>DevTools:E2ECleanupEnabled</c> config flag (config-driven, not the host
/// environment name) so it works in the deployed <c>Dev</c> environment where the post-deploy
/// Playwright smoke gate runs — the older <c>IsDevelopment()</c> check 404'd there. Disabled
/// (returns 404) wherever the flag is unset, e.g. Production.
/// </summary>
public class E2ECleanupEndpoint(
    ApplicationDbContext db, IConfiguration config, IOptions<JobsOptions> jobs, IHostEnvironment env)
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
        // 016-A FR-A6: unavailable in production-like environments regardless of configuration.
        if (env.IsProduction() || env.IsStaging())
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!config.GetValue<bool>("DevTools:E2ECleanupEnabled"))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // 016-A FR-A6: require the shared secret (constant-time), like the scheduler job endpoints.
        var expected = jobs.Value.SchedulerSharedSecret;
        var provided = HttpContext.Request.Headers["X-Scheduler-Secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided ?? string.Empty)))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var deleted = await db.Users
            .Where(u => u.Email != null && EF.Functions.Like(u.Email, "e2e+%@test.dev"))
            .ExecuteDeleteAsync(ct);

        await SendAsync(new { deleted }, 200, ct);
    }
}

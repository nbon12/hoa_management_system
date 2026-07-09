using FastEndpoints;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.DevTools;

// <!-- REPOWISE:START domain=devtools -->
// DELETE /e2e/cleanup — removes e2e+*@test.dev users; 404 unless DevTools:E2ECleanupEnabled.
// Production is refused by a hard environment backstop regardless of the flag (015 US3), and the
// refused attempt is logged as a security-relevant event.
// <!-- REPOWISE:END -->

/// <summary>
/// Test-support endpoint: deletes E2E test users (email LIKE 'e2e+%@test.dev')
/// so that the registration test can run against an unclaimed property each time.
/// Gated on the <c>DevTools:E2ECleanupEnabled</c> config flag (config-driven, not the host
/// environment name) so it works in the deployed <c>Dev</c> environment where the post-deploy
/// Playwright smoke gate runs. Disabled (returns 404) wherever the flag is unset.
/// <para>
/// Defense in depth (015 US3, FR-009): in <c>Production</c> the endpoint behaves as if it does
/// not exist even when the flag is set — startup validation already refuses that configuration,
/// and this guard covers post-boot configuration reloads. The refused invocation is logged as a
/// security-relevant event.
/// </para>
/// </summary>
public class E2ECleanupEndpoint(
    ApplicationDbContext db,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<E2ECleanupEndpoint> logger)
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
        var flagEnabled = config.GetValue<bool>("DevTools:E2ECleanupEnabled");

        // Environment backstop above configuration (015 US3): Production never runs test machinery.
        if (env.IsProduction())
        {
            if (flagEnabled)
                logger.LogWarning(
                    "security devtools.e2e-cleanup refused: invocation attempted in Production with DevTools:E2ECleanupEnabled set from {RemoteIp}",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            await SendNotFoundAsync(ct);
            return;
        }

        if (!flagEnabled)
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

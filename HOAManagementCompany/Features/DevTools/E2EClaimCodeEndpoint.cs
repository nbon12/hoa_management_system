using FastEndpoints;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.DevTools;

/// <summary>
/// Test-support endpoint (020-D FR-D11): issues a fresh claim code for the seed e2e property
/// (SAKURA-003) via the real <see cref="ClaimCodeService"/> — superseding any live code — and
/// returns the raw value so the deployed registration e2e can exercise the full verified flow.
/// Gate parity with <see cref="E2ECleanupEndpoint"/>.
/// </summary>
public class E2EClaimCodeEndpoint(
    ApplicationDbContext db, ClaimCodeService claimCodes,
    IConfiguration config, IOptions<JobsOptions> jobs, IHostEnvironment env)
    : EndpointWithoutRequest
{
    private const string SeedAccountNumber = "SAKURA-003";

    public override void Configure()
    {
        Post("/e2e/claim-code");
        AllowAnonymous();
        Description(x => x.WithTags("DevTools").ExcludeFromDescription());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await E2ETestSupportGate.PassAsync(this, config, jobs.Value, env, ct)) return;

        var property = await db.Properties
            .FirstOrDefaultAsync(p => p.AccountNumber == SeedAccountNumber, ct);
        if (property is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var raw = await claimCodes.IssueAsync(property.Id, "owner-of-sakura-003@seed.local", ct);
        await SendAsync(new { accountNumber = SeedAccountNumber, claimCode = raw }, 200, ct);
    }
}

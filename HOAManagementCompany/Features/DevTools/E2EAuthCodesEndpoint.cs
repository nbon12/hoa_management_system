using FastEndpoints;
using HOAManagementCompany.Features.Payments;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.DevTools;

/// <summary>
/// Test-support endpoint (020-D FR-D11): returns the last verification/claim code the notifier
/// delivered to a contact, from the in-memory <see cref="AuthCodeVault"/>. Gate parity with
/// <see cref="E2ECleanupEndpoint"/>: Production/Staging hard block, DevTools flag, constant-time
/// shared secret.
/// </summary>
public class E2EAuthCodesEndpoint(
    AuthCodeVault vault, IConfiguration config, IOptions<JobsOptions> jobs, IHostEnvironment env)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/e2e/auth-codes");
        AllowAnonymous();
        Description(x => x.WithTags("DevTools").ExcludeFromDescription());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await E2ETestSupportGate.PassAsync(this, config, jobs.Value, env, ct)) return;

        var contact = Query<string>("contact", isRequired: false) ?? "";
        var verification = vault.GetVerification(contact);
        var claim = vault.GetClaim(contact);
        if (verification is null && claim is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new { contact, verificationCode = verification, claimCode = claim }, 200, ct);
    }
}

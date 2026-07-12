using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Features.DevTools;

/// <summary>
/// The single gate every e2e test-support endpoint passes (016-A FR-A6 / 020-D FR-D11):
/// 1. hard 404 in Production/Staging regardless of configuration;
/// 2. 404 unless <c>DevTools:E2ECleanupEnabled</c>;
/// 3. 401 unless <c>X-Scheduler-Secret</c> matches (constant-time).
/// Returns false when a response has already been sent.
/// </summary>
public static class E2ETestSupportGate
{
    public static async Task<bool> PassAsync(
        IEndpoint endpoint, IConfiguration config, JobsOptions jobs, IHostEnvironment env, CancellationToken ct)
    {
        var http = endpoint.HttpContext;

        if (env.IsProduction() || env.IsStaging())
        {
            await http.Response.SendNotFoundAsync(ct);
            return false;
        }

        if (!config.GetValue<bool>("DevTools:E2ECleanupEnabled"))
        {
            await http.Response.SendNotFoundAsync(ct);
            return false;
        }

        var expected = jobs.SchedulerSharedSecret;
        var provided = http.Request.Headers["X-Scheduler-Secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided ?? string.Empty)))
        {
            await http.Response.SendUnauthorizedAsync(ct);
            return false;
        }

        return true;
    }
}

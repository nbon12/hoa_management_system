using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using HOAManagementCompany.Infrastructure.Configuration;

namespace HOAManagementCompany.Features.Payments.Jobs;

/// <summary>
/// Single definition of the Cloud Scheduler shared-secret check (015 US5, FR-015 — previously
/// duplicated verbatim in RunDraftsEndpoint and ReconcileEndpoint). Constant-time comparison of
/// the <c>X-Scheduler-Secret</c> header against <see cref="JobsOptions.SchedulerSharedSecret"/>;
/// an unset secret always refuses.
/// </summary>
public static class SchedulerAuth
{
    public const string HeaderName = "X-Scheduler-Secret";

    public static bool IsAuthorized(HttpContext context, IOptions<JobsOptions> options)
    {
        var expected = options.Value.SchedulerSharedSecret;
        var provided = context.Request.Headers[HeaderName].FirstOrDefault();
        return !string.IsNullOrEmpty(expected) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided ?? string.Empty));
    }
}

using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using HOAManagementCompany.Features.Payments;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// POST /architectural/jobs/sweep — Cloud Scheduler-triggered (hourly) reminder, lapse and email
/// dispatch sweep (027 research R6). Authenticated by the shared <c>X-Scheduler-Secret</c> header,
/// not a user session; on the BoardScopeEnforcement allow-list for that reason. Idempotent.
/// </summary>
public class ArcSweepJobEndpoint(ArcSweepService sweep, IOptions<JobsOptions> options)
    : EndpointWithoutRequest<ArcSweepResultDto>
{
    public override void Configure()
    {
        Post("/architectural/jobs/sweep");
        AllowAnonymous();
        Description(x => x.WithName("RunArchitecturalSweep").WithTags("Board"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var expected = options.Value.SchedulerSharedSecret;
        var provided = HttpContext.Request.Headers["X-Scheduler-Secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided ?? string.Empty)))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await sweep.RunAsync(ct);
        Logger.LogInformation(
            "audit architectural.jobs.sweep reminders {Reminders} lapses {Lapses} decisions {Decisions} dispatched {Dispatched}",
            result.RemindersQueued, result.LapsesProcessed, result.DecisionsAtDueDate, result.EmailsDispatched);
        await SendOkAsync(result, ct);
    }
}

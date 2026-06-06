using FastEndpoints;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Payments.Jobs;

/// <summary>
/// POST /payments/jobs/reconcile — Cloud Scheduler-triggered backstop (FR-033). Resolves stuck ACH
/// transactions against Stripe and retries undelivered webhook events. Authenticated by a shared
/// secret header (<c>X-Scheduler-Secret</c>), not a user session. Returns counts for observability.
/// </summary>
public class ReconcileEndpoint(ReconciliationService reconciliation, IOptions<JobsOptions> options)
    : EndpointWithoutRequest<ReconcileResponse>
{
    public override void Configure()
    {
        Post("/payments/jobs/reconcile");
        AllowAnonymous();
        Description(x => x.WithName("ReconcilePayments").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var expected = options.Value.SchedulerSharedSecret;
        var provided = HttpContext.Request.Headers["X-Scheduler-Secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(expected) || !string.Equals(expected, provided, StringComparison.Ordinal))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var resolvedAch = await reconciliation.ResolvePendingAchAsync(ct);
        var retriedWebhooks = await reconciliation.RetryPendingWebhooksAsync(ct);

        await SendOkAsync(new ReconcileResponse(resolvedAch, retriedWebhooks), ct);
    }
}

public record ReconcileResponse(int ResolvedAchTransactions, int RetriedWebhooks);

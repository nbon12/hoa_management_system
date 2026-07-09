using HOAManagementCompany.Infrastructure.Configuration;
using FastEndpoints;
using HOAManagementCompany.Features.Payments.Alerts;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Payments.Jobs;

/// <summary>
/// POST /payments/jobs/reconcile — Cloud Scheduler-triggered backstop (FR-033). Resolves stuck ACH
/// transactions against Stripe, retries undelivered webhook events, and flushes any pending outbox
/// alerts the prompt in-process dispatch missed (FR-034). Authenticated by a shared secret header
/// (<c>X-Scheduler-Secret</c>), not a user session. Returns counts for observability.
/// </summary>
public class ReconcileEndpoint(
    ReconciliationService reconciliation, OutboxDispatcher dispatcher, IOptions<JobsOptions> options)
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
        if (!SchedulerAuth.IsAuthorized(HttpContext, options))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var resolvedAch = await reconciliation.ResolvePendingAchAsync(ct);
        var retriedWebhooks = await reconciliation.RetryPendingWebhooksAsync(ct);
        var dispatchedAlerts = await dispatcher.DispatchPendingAsync(ct);
        // Report-only status/ledger consistency scan (015 FR-005) — findings go to logs/Sentry.
        var inconsistencies = await reconciliation.DetectLedgerInconsistenciesAsync(ct);

        await SendOkAsync(new ReconcileResponse(resolvedAch, retriedWebhooks, dispatchedAlerts, inconsistencies.Count), ct);
    }
}

public record ReconcileResponse(int ResolvedAchTransactions, int RetriedWebhooks, int DispatchedAlerts, int LedgerInconsistencies);

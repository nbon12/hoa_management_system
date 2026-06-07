using FastEndpoints;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Payments.Recurring;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Payments.Jobs;

/// <summary>
/// POST /payments/jobs/run-drafts — Cloud Scheduler-triggered recurring auto-pay sweep (FR-010).
/// Charges every active mandate due today against its vaulted method off-session. Authenticated by
/// the shared <c>X-Scheduler-Secret</c> header (not a user session). Idempotent per recurring/period,
/// so a same-day re-run is safe.
/// </summary>
public class RunDraftsEndpoint(
    RecurringDraftService draftService, VariableNoticeService noticeService, IOptions<JobsOptions> options)
    : EndpointWithoutRequest<RunDraftsResponse>
{
    public override void Configure()
    {
        Post("/payments/jobs/run-drafts");
        AllowAnonymous();
        Description(x => x.WithName("RunRecurringDrafts").WithTags("Payments"));
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

        // Optional ?date=YYYY-MM-DD override for backfills/tests; defaults to today (UTC).
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var dateParam = Query<string?>("date", isRequired: false);
        if (!string.IsNullOrWhiteSpace(dateParam) && DateOnly.TryParse(dateParam, out var parsed))
            asOf = parsed;

        // Advance notice for upcoming variable-amount drafts (FR-011c), then charge today's due drafts.
        var noticesSent = await noticeService.SendDueNoticesAsync(asOf, ct);
        var result = await draftService.RunDueDraftsAsync(asOf, ct);
        await SendOkAsync(result with { NoticesSent = noticesSent }, ct);
    }
}

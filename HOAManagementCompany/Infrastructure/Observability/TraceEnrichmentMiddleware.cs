// <!-- REPOWISE:START domain=observability -->
// Log correlation (FR-003/FR-011). ActivityTraceEnricher stamps every log record with
// the current trace_id/span_id so any entry can be tied back to its request. The
// TraceEnrichmentMiddleware pushes the authenticated user's subject GUID
// (ClaimTypes.NameIdentifier) — never the email/username — into the Serilog LogContext
// for the request scope so request-scoped log entries identify the user safely.
// <!-- REPOWISE:END -->

using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace HOAManagementCompany.Infrastructure.Observability;

/// <summary>
/// Serilog enricher that attaches the current Activity's trace_id and span_id to every
/// log event, so logs emitted during a request correlate to its trace (FR-003).
/// </summary>
public sealed class ActivityTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("trace_id", activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("span_id", activity.SpanId.ToHexString()));
    }
}

/// <summary>
/// Attaches the authenticated user's subject GUID to the log context for the request
/// scope. Registered after authentication and outside request logging so every
/// request-scoped entry carries <c>user_id</c> (FR-011). Email/username are never logged.
/// </summary>
public sealed class TraceEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var subjectId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(subjectId))
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty("user_id", subjectId))
            await next(context);
    }
}

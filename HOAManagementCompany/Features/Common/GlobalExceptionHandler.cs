// <!-- REPOWISE:START domain=shared-kernel -->
// Global exception handler — the ONLY sanctioned business-error translation (015 FR-006):
// DomainException maps to the uniform { code, message } envelope at its intended status, so
// endpoints raise and never translate; a new endpoint gets contract-correct errors with zero
// boilerplate. Unhandled exceptions keep the { code, message, detail } 500 envelope, with
// `detail` config-gated via DevToolsOptions.ExposeExceptionDetail (014 US3) and forced off in
// Production, so production responses never leak stack traces or internal paths (FR-009/SC-007).
// <!-- REPOWISE:END -->

using HOAManagementCompany.Domain;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Common;

public class GlobalExceptionHandler(
    IOptions<DevToolsOptions> devToolsOptions,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private readonly bool _exposeDetail = devToolsOptions.Value.ExposeExceptionDetail ?? false;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        // Known business error (015 FR-006): uniform envelope, intended status, no Sentry noise —
        // logged as information because it is an expected, user-presentable outcome.
        if (exception is DomainException domain)
        {
            logger.LogInformation(
                "Business error {Code} ({StatusCode}) on {Path}: {Message}",
                domain.Code, domain.StatusCode, context.Request.Path, domain.Message);

            context.Response.StatusCode = domain.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { code = domain.Code, message = domain.Message }, ct);
            return true;
        }

        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = _exposeDetail ? exception.ToString() : null
        };

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(
            new { code = "INTERNAL_ERROR", message = problem.Title, detail = problem.Detail },
            ct);

        return true;
    }
}

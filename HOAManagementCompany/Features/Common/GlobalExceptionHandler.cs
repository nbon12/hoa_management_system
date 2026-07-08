// <!-- REPOWISE:START domain=error-handling -->
// Global exception handler: logs the unhandled exception server-side and returns a consistent,
// client-safe error envelope ({ code, message, detail }). Whether `detail` carries the full
// exception text is config-gated via DevToolsOptions.ExposeExceptionDetail (014 US3) instead of the
// old host-name check (env.IsDevelopment()), which silently returned no detail in the deployed `Dev`
// environment. The flag defaults to dev-like and is forced off in Production, so production responses
// never leak stack traces or internal paths (FR-009/SC-007).
// <!-- REPOWISE:END -->

using HOAManagementCompany.Features.Auth;
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
        // 017-A FR-A8: DomainException carries a deliberate, client-safe status/code — surface it
        // instead of converting it to a 500. Endpoints that catch it locally are unaffected.
        if (exception is DomainException domainEx)
        {
            logger.LogWarning("Domain error {Code} ({StatusCode}): {Message}",
                domainEx.Code, domainEx.StatusCode, domainEx.Message);

            context.Response.StatusCode = domainEx.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                new { code = domainEx.Code, message = domainEx.Message }, ct);
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

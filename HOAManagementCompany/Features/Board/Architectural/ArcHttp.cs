using Microsoft.AspNetCore.Http;

namespace HOAManagementCompany.Features.Board.Architectural;

// Shared error writer for architectural endpoints. Uses the app-wide { code, message } shape;
// scope denials go through BoardHttp.ForbiddenAsync so they match 025's non-disclosing body.
internal static class ArcHttp
{
    public static async Task ErrorAsync(HttpContext ctx, int status, string code, string message, CancellationToken ct)
    {
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { code, message }, ct);
    }

    public static Task ValidationAsync(HttpContext ctx, string message, CancellationToken ct) =>
        ErrorAsync(ctx, StatusCodes.Status422UnprocessableEntity, ArcErrorCodes.ValidationError, message, ct);

    public static Task ConflictAsync(HttpContext ctx, string code, string message, CancellationToken ct) =>
        ErrorAsync(ctx, StatusCodes.Status409Conflict, code, message, ct);
}

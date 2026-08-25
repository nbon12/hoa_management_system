using Microsoft.AspNetCore.Http;

namespace HOAManagementCompany.Features.Board;

// Shared board-endpoint HTTP helpers. The 403 body is deliberately identical for
// "not permitted" and "community does not exist" so a denial never discloses
// whether an out-of-scope community exists (spec FR-016).
internal static class BoardHttp
{
    public static async Task ForbiddenAsync(HttpContext ctx, CancellationToken ct)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(
            new { code = "FORBIDDEN", message = "You do not have access to this community." }, ct);
    }

    // Every board response is authenticated and community-specific — never edge-cached
    // (constitution §8). Call before writing the body.
    public static void NoStore(HttpContext ctx) => ctx.Response.Headers.CacheControl = "no-store";
}

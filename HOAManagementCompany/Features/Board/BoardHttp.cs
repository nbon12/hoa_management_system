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

    // 030 FR-004a: write the refusal a scope decision calls for, with a plain-language message
    // (FR-042). Forbidden keeps the non-disclosing 025 body. Returns false when the caller may proceed.
    public static async Task<bool> SendDecisionAsync(HttpContext ctx, ScopeDecision decision, CancellationToken ct)
    {
        switch (decision)
        {
            case ScopeDecision.Allowed:
                return false;
            case ScopeDecision.Archived:
                await ErrorAsync(ctx, StatusCodes.Status409Conflict, "COMMUNITY_ARCHIVED",
                    "This community is archived, so it can't be changed.", ct);
                return true;
            case ScopeDecision.NotActive:
                await ErrorAsync(ctx, StatusCodes.Status409Conflict, "COMMUNITY_NOT_ACTIVE",
                    "This community isn't live yet.", ct);
                return true;
            default:
                await ForbiddenAsync(ctx, ct);
                return true;
        }
    }

    public static async Task ErrorAsync(HttpContext ctx, int status, string code, string message, CancellationToken ct)
    {
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { code, message }, ct);
    }

    // Every board response is authenticated and community-specific — never edge-cached
    // (constitution §8). Call before writing the body.
    public static void NoStore(HttpContext ctx) => ctx.Response.Headers.CacheControl = "no-store";
}

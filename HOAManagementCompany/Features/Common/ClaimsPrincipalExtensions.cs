using System.Security.Claims;
using HOAManagementCompany.Domain;

namespace HOAManagementCompany.Features.Common;

// <!-- REPOWISE:START domain=shared-kernel -->
// Single identity-claim accessor (015 FR-008): replaces the inline null-forgiving
// claim-parse pattern that NREd into a 500 when a claim was absent. A missing/invalid
// claim now raises MISSING_CLAIM, which the central error mapping turns into a clean 403.
// <!-- REPOWISE:END -->

/// <summary>
/// The one sanctioned way for endpoints to read identity attributes off the authenticated
/// principal. Missing or malformed claims surface as a <see cref="DomainException"/> with code
/// <c>MISSING_CLAIM</c> (403) through the global exception handler — never a null dereference.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>The property the session is scoped to.</summary>
    public static Guid GetPropertyId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("propertyId")?.Value, out var id)
            ? id
            : throw MissingClaim("propertyId");

    /// <summary>The community (HOA) the session's property belongs to.</summary>
    public static string GetCommunityId(this ClaimsPrincipal user) =>
        user.FindFirst("communityId")?.Value is { Length: > 0 } id
            ? id
            : throw MissingClaim("communityId");

    /// <summary>The authenticated user id (NameIdentifier, falling back to <c>sub</c>).</summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        (user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value)
            is { Length: > 0 } id
            ? id
            : throw MissingClaim("sub");

    private static DomainException MissingClaim(string claim) => new(
        "MISSING_CLAIM",
        $"The session is missing the required '{claim}' attribute. Sign in again.",
        StatusCodes.Status403Forbidden);
}

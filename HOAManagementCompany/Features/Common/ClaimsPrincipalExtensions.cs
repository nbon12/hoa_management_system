using System.Security.Claims;
using HOAManagementCompany.Features.Auth;

namespace HOAManagementCompany.Features.Common;

// 017-A FR-A8: defensive claim reads. A validly-signed token missing a required identity claim
// (e.g. minted before a schema change, or a service token) must yield a clean 401, not a
// NullReferenceException-driven 500. Message stays generic — no oracle on which claim was absent.
public static class ClaimsPrincipalExtensions
{
    public static Guid RequirePropertyId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("propertyId")?.Value;
        if (!Guid.TryParse(value, out var propertyId))
            throw new DomainException("UNAUTHORIZED", "Token is missing required identity claims.", StatusCodes.Status401Unauthorized);
        return propertyId;
    }

    public static string RequireCommunityId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("communityId")?.Value;
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("UNAUTHORIZED", "Token is missing required identity claims.", StatusCodes.Status401Unauthorized);
        return value;
    }
}

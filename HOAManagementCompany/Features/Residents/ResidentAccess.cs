using System.Security.Claims;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Residents;

/// <summary>What a resident endpoint does with the selected home (030 R8).</summary>
public enum ResidentOperation
{
    /// <summary>Any read other than the history below.</summary>
    Read,

    /// <summary>Payment history (ledger, statements, receipts, transactions, unpaid assessments)
    /// and the home's submitted architectural requests — still readable when Archived.</summary>
    ReadHistory,

    /// <summary>Any change.</summary>
    Write
}

/// <summary>The caller's home as stored now — use these ids, never the token's (FR-012a).</summary>
public sealed record ResidentContext(string UserId, Guid PropertyId, Guid CommunityId, CommunityStatus CommunityStatus);

// <!-- REPOWISE:START domain=residents -->
// The per-request resident check (030 FR-024g, FR-012a; research R8). Every resident endpoint calls
// RequireAsync first. The token's propertyId claim only says which home is selected; the check
// re-reads the resident link and the home's community status from the database on every request,
// so a removed resident loses the home on their very next request (SC-011) and a status change
// applies at once. Outcomes: no claim or no link → 403 PROPERTY_ACCESS_DENIED; Onboarding → 409
// COMMUNITY_NOT_ACTIVE; Archived → 409 COMMUNITY_ARCHIVED except for history reads. One indexed
// lookup on the unique (UserId, PropertyId) link.
// <!-- REPOWISE:END -->

public sealed class ResidentAccess(ApplicationDbContext db)
{
    public async Task<ResidentContext> RequireAsync(ClaimsPrincipal user, ResidentOperation operation, CancellationToken ct)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(user.FindFirst("propertyId")?.Value, out var propertyId))
            throw CompanyErrors.PropertyAccessDenied();
        return await RecheckAsync(db, userId, propertyId, operation, ct);
    }

    /// <summary>
    /// The same check for a known user and home, for a write that must check again under a lock
    /// (for example after slow processor calls), so a removal or archive in between wins.
    /// </summary>
    public static async Task<ResidentContext> RecheckAsync(ApplicationDbContext db, string userId, Guid propertyId,
        ResidentOperation operation, CancellationToken ct)
    {
        var home = await db.UserProperties
            .Where(up => up.UserId == userId && up.PropertyId == propertyId)
            .Select(up => new
            {
                up.Property.CommunityId,
                up.Property.Community.Status,
                up.Property.Community.CommunityName,
                up.Property.Community.LegalName,
                CompanyName = up.Property.Community.ManagementCompany.Name,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw CompanyErrors.PropertyAccessDenied();

        if (home.Status == CommunityStatus.Onboarding)
            throw CompanyErrors.CommunityNotActive();

        if (home.Status == CommunityStatus.Archived && operation != ResidentOperation.ReadHistory)
        {
            var community = string.IsNullOrWhiteSpace(home.CommunityName) ? home.LegalName : home.CommunityName;
            throw new Auth.DomainException("COMMUNITY_ARCHIVED",
                operation == ResidentOperation.Write
                    ? $"{home.CompanyName} no longer manages {community}, so this home can't be changed here. You can still view its ledger, statements, receipts and transactions."
                    : $"{home.CompanyName} no longer manages {community}. You can still view this home's ledger, statements, receipts and transactions.",
                StatusCodes.Status409Conflict);
        }

        return new ResidentContext(userId, propertyId, home.CommunityId, home.Status);
    }
}

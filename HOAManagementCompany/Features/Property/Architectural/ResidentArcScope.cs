using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Features.Residents;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Property.Architectural;

/// <summary>
/// The single resident authorization point for architectural requests (029 FR-022–FR-024).
/// Every draft and application is loaded pinned to the caller's selected home, which 030's
/// <see cref="ResidentAccess"/> re-checks on every request from stored data: the resident link and
/// the home's community status (030 FR-024g, FR-012a). Submitted requests stay readable when the
/// community is archived (ReadHistory); drafts and writes don't. Board or manager memberships are
/// never consulted, so they can't widen access (FR-023). "Not yours" and "doesn't exist" get the
/// same 403 body, so a denial never reveals whether a request exists.
/// </summary>
public sealed class ResidentArcScope(ApplicationDbContext db, ResidentAccess access, ILogger<ResidentArcScope> logger)
{
    public const string ForbiddenMessage = "You do not have access to this request.";

    /// <summary>The caller's home, checked for <paramref name="operation"/> (030 FR-024g).</summary>
    public Task<ResidentContext> HomeAsync(ClaimsPrincipal user, ResidentOperation operation, CancellationToken ct) =>
        access.RequireAsync(user, operation, ct);

    public static string UserId(ClaimsPrincipal user) => ArcQueries.CallerId(user);

    public async Task<ArchitecturalApplicationDraft> DraftAsync(
        ClaimsPrincipal user, Guid draftId, ResidentOperation operation, CancellationToken ct)
    {
        var propertyId = (await HomeAsync(user, operation, ct)).PropertyId;
        var draft = await db.ArchitecturalApplicationDrafts
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == draftId && d.PropertyId == propertyId, ct);
        return draft ?? throw Deny(user, propertyId, $"draft:{draftId}");
    }

    public async Task<ArchitecturalApplication> ApplicationAsync(
        ClaimsPrincipal user, Guid applicationId, ResidentOperation operation, CancellationToken ct)
    {
        var propertyId = (await HomeAsync(user, operation, ct)).PropertyId;
        var app = await db.ArchitecturalApplications
            .Include(a => a.Attachments)
            .Include(a => a.InfoRequests)
            .Include(a => a.OwnerComments)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.PropertyId == propertyId, ct);
        return app ?? throw Deny(user, propertyId, $"application:{applicationId}");
    }

    private DomainException Deny(ClaimsPrincipal user, Guid propertyId, string resource)
    {
        ResidentArcLog.AccessDenied(logger, UserId(user), propertyId, resource);
        return Forbidden();
    }

    public static DomainException Forbidden() =>
        new(ResidentArcErrorCodes.Forbidden, ForbiddenMessage, StatusCodes.Status403Forbidden);

    public static DomainException NotFound(string what) =>
        new(ResidentArcErrorCodes.NotFound, $"{what} was not found.", StatusCodes.Status404NotFound);
}

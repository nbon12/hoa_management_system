using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HOAManagementCompany.Features.Company;

public sealed record CreateCommunityRequest(
    string? LegalName,
    string? CommunityName,
    string? County,
    DateOnly? FormationDate,
    DateOnly? ManagementStartDate,
    string? Description,
    Guid? ParentCommunityId);

public sealed record CreateCommunityResponse(Guid Id, string CommunityName, string Status, string SettingsPath);

/// <summary>
/// POST /communities — add a community the company has taken on (030 US1; FR-006–FR-009, FR-016;
/// contracts/company.md). It starts Onboarding with the company's default ARC settings. A
/// Community Manager who adds it becomes its open-ended manager, and every Company Administrator
/// of the company is emailed.
/// </summary>
public class CommunityCreateEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ICompanyContext companyContext,
    CompanyEmailRenderer emails,
    OutboxDispatcher dispatcher,
    TimeProvider clock,
    ILogger<CommunityCreateEndpoint> logger) : Endpoint<CreateCommunityRequest, CreateCommunityResponse>
{
    private const string NameIndex = "IX_Communities_ManagementCompanyId_NormalizedName";

    public override void Configure()
    {
        Post("/communities");
        Description(x => x.WithName("CreateCommunity").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CreateCommunityRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessCompanyAsync(User, CompanyCapability.CreateCommunity, ct)
            || await companyContext.ForUserAsync(User, ct) is not { } companyId)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var now = clock.GetUtcNow();
        var errors = new[]
        {
            CommunityFieldRules.LegalName(req.LegalName),
            CommunityFieldRules.CommunityName(req.CommunityName),
            CommunityFieldRules.County(req.County),
            CommunityFieldRules.FormationDate(req.FormationDate, DateOnly.FromDateTime(now.UtcDateTime)),
            CommunityFieldRules.ManagementStartDate(req.ManagementStartDate),
            CommunityFieldRules.Description(req.Description),
        }.OfType<CompanyErrors.FieldError>().ToArray();
        if (errors.Length > 0)
            throw CompanyErrors.Validation(errors);

        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;
        var isAdministrator = await scope.IsCompanyAdministratorAsync(User, companyId, ct);

        if (req.ParentCommunityId is { } parentId && !await ParentAllowedAsync(parentId, companyId, isAdministrator, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var name = req.CommunityName!.Trim();
        var normalized = Domain.Entities.Community.Normalize(name);
        if (await db.Communities.AsNoTracking()
                .Where(c => c.ManagementCompanyId == companyId && c.NormalizedName == normalized)
                .Select(c => new { c.CommunityName, c.Status })
                .FirstOrDefaultAsync(ct) is { } taken)
            throw CompanyErrors.CommunityNameTaken(isAdministrator ? taken.CommunityName : null, taken.Status == CommunityStatus.Archived);

        var community = new Domain.Entities.Community
        {
            Id = Guid.NewGuid(),
            ManagementCompanyId = companyId,
            CommunityName = name,
            LegalName = req.LegalName!.Trim(),
            County = req.County!.Trim(),
            FormationDate = req.FormationDate,
            ManagementStartDate = req.ManagementStartDate,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            ParentCommunityId = req.ParentCommunityId,
            Status = CommunityStatus.Onboarding,
            CreatedAt = now,
            CreatedByUserId = actorId,
        };
        db.Communities.Add(community);

        // FR-009: the copy is made now; later default changes never reach it.
        var defaults = await db.CompanyDefaultSettings.AsNoTracking().FirstOrDefaultAsync(d => d.CompanyId == companyId, ct);
        db.CommunityArcSettings.Add(defaults?.CopyTo(community.Id) ?? CommunityArcSettings.Defaults(community.Id));

        CommunityMembership? membership = null;
        if (!isAdministrator)
        {
            // FR-008: the manager who adds it is its first, open-ended manager, and every Company
            // Administrator is told it is waiting to be marked Active. The notices commit with the
            // community, so they can't be lost.
            membership = new CommunityMembership
            {
                Id = Guid.NewGuid(),
                UserId = actorId,
                CommunityId = community.Id,
                Role = CommunityRole.CommunityManager,
                Status = MembershipStatus.Active,
                StartDate = DateOnly.FromDateTime(now.UtcDateTime),
            };
            db.CommunityMemberships.Add(membership);
            var managerName = await db.Users.Where(u => u.Id == actorId)
                .Select(u => u.FirstName + " " + u.LastName).FirstAsync(ct);
            var administrators = await db.CompanyAdministratorGrants.AsNoTracking()
                .Where(g => g.CompanyId == companyId && g.RevokedAt == null)
                .Select(g => new { g.UserId, g.User.Email })
                .ToListAsync(ct);
            foreach (var admin in administrators.Where(a => a.Email is not null).DistinctBy(a => a.UserId))
                db.OutboxMessages.Add(emails.CommunityAdded(admin.UserId, admin.Email!, community.Id, name, managerName.Trim()));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameIndex })
        {
            // Another request took the name between the check and the insert.
            throw CompanyErrors.CommunityNameTaken(isAdministrator ? name : null, otherIsArchived: false);
        }

        CompanyLog.CommunityLifecycle(logger, actorId, community.Id, "Created", null, nameof(CommunityStatus.Onboarding), now);
        if (membership is not null)
            CompanyLog.MembershipChange(logger, actorId, community.Id, membership.Id, actorId,
                nameof(CommunityRole.CommunityManager), "grant", "CommunityCreated", now);
        await dispatcher.DispatchPendingAsync(ct);

        var settingsPath = isAdministrator
            ? $"/app/company/communities/{community.Id}/settings/profile"
            : "/app/board/settings/profile";
        await SendAsync(new CreateCommunityResponse(community.Id, name, nameof(CommunityStatus.Onboarding), settingsPath),
            StatusCodes.Status201Created, ct);
    }

    // FR-016: a Company Administrator may choose any non-archived community of the company; a
    // Community Manager only one they manage. Anything else gets the same 403 as a parent that
    // doesn't exist.
    private async Task<bool> ParentAllowedAsync(Guid parentId, Guid companyId, bool isAdministrator, CancellationToken ct)
    {
        var parent = await db.Communities.AsNoTracking()
            .Where(c => c.Id == parentId)
            .Select(c => new { c.ManagementCompanyId, c.Status, c.CommunityName })
            .FirstOrDefaultAsync(ct);
        if (parent is null || parent.ManagementCompanyId != companyId)
            return false;
        if (!isAdministrator)
            return await scope.DecideAsync(User, parentId, CommunityCapability.ManageCommunityManagers,
                ScopeOperation.Read, ct) == ScopeDecision.Allowed;
        if (parent.Status == CommunityStatus.Archived)
            throw CompanyErrors.ParentArchived(parent.CommunityName);
        return true;
    }
}

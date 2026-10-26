using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

public sealed class PortfolioQuery
{
    public string? Status { get; set; }
    public bool? NoManager { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed record PortfolioManager(string UserId, string Name);

public sealed record PendingManagerInvitation(string Email, DateTimeOffset? ExpiresAt);

public sealed record PortfolioItem(
    Guid Id, string CommunityName, string LegalName, string Status, ParentSummary? Parent,
    IReadOnlyList<PortfolioManager> Managers, IReadOnlyList<PendingManagerInvitation> PendingManagerInvitations,
    bool NoManager, bool DiffersFromDefaults);

public sealed record PortfolioCounts(int All, int Onboarding, int Active, int Archived, int NoManager);

public sealed record PortfolioResponse(
    IReadOnlyList<PortfolioItem> Items, int Total, int Limit, int Offset, PortfolioCounts Counts);

/// <summary>
/// GET /company/communities — the portfolio (030 US6; FR-034, FR-035). The caller's company only,
/// paged, with each community's open-ended managers, pending manager invitations, the "No
/// manager" and "differs from defaults" flags, the status, no-manager and search filters, and
/// sorting by name, status or parent. One SQL projection (R17). Company Administrators only.
/// </summary>
public class PortfolioEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, TimeProvider clock)
    : Endpoint<PortfolioQuery, PortfolioResponse>
{
    public const int MaxSearchLength = 100;

    public override void Configure()
    {
        Get("/company/communities");
        Description(x => x.WithName("Portfolio").WithTags("Company"));
    }

    public override async Task HandleAsync(PortfolioQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessCompanyAsync(User, CompanyCapability.ViewPortfolio, ct)
            || await companyContext.ForUserAsync(User, ct) is not { } companyId)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        CommunityStatus? status = req.Status?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "onboarding" => CommunityStatus.Onboarding,
            "active" => CommunityStatus.Active,
            "archived" => CommunityStatus.Archived,
            _ => throw CompanyErrors.Validation(new CompanyErrors.FieldError("status", "status must be onboarding, active or archived.")),
        };
        var search = string.IsNullOrWhiteSpace(req.Search) ? null : req.Search.Trim().ToLowerInvariant();
        if (search is { Length: > MaxSearchLength })
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("search", $"Search can be at most {MaxSearchLength} characters."));
        var sort = req.Sort?.Trim().ToLowerInvariant() ?? "name";
        if (sort is not ("name" or "status" or "parent"))
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("sort", "sort must be name, status or parent."));
        var descending = (req.Dir?.Trim().ToLowerInvariant() ?? "asc") switch
        {
            "asc" => false,
            "desc" => true,
            _ => throw CompanyErrors.Validation(new CompanyErrors.FieldError("dir", "dir must be asc or desc.")),
        };
        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var now = clock.GetUtcNow();

        var d = await db.CompanyDefaultSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId, ct)
                ?? new CompanyDefaultSettings();
        var communities = db.Communities.AsNoTracking().Where(c => c.ManagementCompanyId == companyId);
        // No open-ended Community Manager membership (ManagerRule.Row.IsOpenEnded); a pending invitation doesn't count.
        System.Linq.Expressions.Expression<Func<Domain.Entities.Community, bool>> noManager = c => !c.Memberships.Any(m =>
            m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active && m.EndDate == null);

        // The counts ignore the search, the filters and paging.
        var byStatus = await communities.GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(CommunityStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        var counts = new PortfolioCounts(byStatus.Sum(x => x.Count), Count(CommunityStatus.Onboarding),
            Count(CommunityStatus.Active), Count(CommunityStatus.Archived), await communities.CountAsync(noManager, ct));

        var filtered = communities;
        if (status is { } wanted)
            filtered = filtered.Where(c => c.Status == wanted);
        if (req.NoManager == true)
            filtered = filtered.Where(noManager);
        if (search is not null)
            filtered = filtered.Where(c => c.NormalizedName.Contains(search) || c.LegalName.ToLower().Contains(search));
        var total = await filtered.CountAsync(ct);

        IOrderedQueryable<Domain.Entities.Community> ordered = sort switch
        {
            "status" => descending
                ? filtered.OrderByDescending(c => c.Status == CommunityStatus.Onboarding ? 0 : c.Status == CommunityStatus.Active ? 1 : 2)
                : filtered.OrderBy(c => c.Status == CommunityStatus.Onboarding ? 0 : c.Status == CommunityStatus.Active ? 1 : 2),
            "parent" => descending
                ? filtered.OrderByDescending(c => EF.Functions.Collate(c.ParentCommunity!.NormalizedName ?? "", "natural_sort"))
                : filtered.OrderBy(c => EF.Functions.Collate(c.ParentCommunity!.NormalizedName ?? "", "natural_sort")),
            _ => descending
                ? filtered.OrderByDescending(c => EF.Functions.Collate(c.NormalizedName, "natural_sort"))
                : filtered.OrderBy(c => EF.Functions.Collate(c.NormalizedName, "natural_sort")),
        };
        if (sort != "name")
            ordered = descending
                ? ordered.ThenByDescending(c => EF.Functions.Collate(c.NormalizedName, "natural_sort"))
                : ordered.ThenBy(c => EF.Functions.Collate(c.NormalizedName, "natural_sort"));

        var items = await ordered
            .ThenBy(c => c.Id)
            .Skip(offset)
            .Take(limit)
            .Select(c => new PortfolioItem(
                c.Id,
                c.CommunityName,
                c.LegalName,
                c.Status.ToString(),
                c.ParentCommunity == null ? null : new ParentSummary(c.ParentCommunity.Id, c.ParentCommunity.CommunityName),
                c.Memberships
                    .Where(m => m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active && m.EndDate == null)
                    .OrderBy(m => m.User.FirstName).ThenBy(m => m.User.LastName)
                    .Select(m => new PortfolioManager(m.UserId, (m.User.FirstName + " " + m.User.LastName).Trim()))
                    .ToList(),
                db.Invitations
                    .Where(i => i.CommunityId == c.Id && i.Kind == InvitationKind.Membership && i.Role == CommunityRole.CommunityManager
                                && i.State == InvitationState.Pending && i.ExpiresAt > now)
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new PendingManagerInvitation(i.Email, i.ExpiresAt))
                    .ToList(),
                !c.Memberships.Any(m =>
                    m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active && m.EndDate == null),
                // R13: the six ARC rule fields against the company defaults.
                db.CommunityArcSettings.Any(s => s.CommunityId == c.Id
                    && (s.ReviewPeriodDays != d.ReviewPeriodDays || s.LapseRule != d.LapseRule || s.DecisionRule != d.DecisionRule
                        || s.ReminderDays != d.ReminderDays || s.TimeZoneId != d.TimeZoneId
                        || s.FormalDisapprovalStatement != d.FormalDisapprovalStatement))))
            .ToListAsync(ct);

        await SendOkAsync(new PortfolioResponse(items, total, limit, offset, counts), ct);
    }
}

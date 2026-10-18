using FastEndpoints;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

public sealed class PortfolioQuery
{
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed record PortfolioItem(Guid Id, string CommunityName, string LegalName, string Status, ParentSummary? Parent);

/// <summary>
/// GET /company/communities — the portfolio (030 US1 first version; US6 adds flags, filters,
/// search and sorting). The caller's company only, A–Z by name in natural order, paged.
/// Company Administrators only (ViewPortfolio).
/// </summary>
public class PortfolioEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext)
    : Endpoint<PortfolioQuery, PagedResponse<PortfolioItem>>
{
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

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var communities = db.Communities.AsNoTracking().Where(c => c.ManagementCompanyId == companyId);
        var total = await communities.CountAsync(ct);
        var items = await communities
            .OrderBy(c => EF.Functions.Collate(c.NormalizedName, "natural_sort"))
            .ThenBy(c => c.Id)
            .Skip(offset)
            .Take(limit)
            .Select(c => new PortfolioItem(
                c.Id,
                c.CommunityName,
                c.LegalName,
                c.Status.ToString(),
                c.ParentCommunity == null ? null : new ParentSummary(c.ParentCommunity.Id, c.ParentCommunity.CommunityName)))
            .ToListAsync(ct);

        await SendOkAsync(new PagedResponse<PortfolioItem>(items, total, limit, offset), ct);
    }
}

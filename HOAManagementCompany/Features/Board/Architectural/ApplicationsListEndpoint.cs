using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// GET /communities/{communityId}/architectural-applications — the Open/Closed list, search and the
/// Needs-your-vote feed (027 US1, US5; FR-002–FR-009, FR-032). Board members and managers only.
/// </summary>
public class ApplicationsListEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ArcQueries queries)
    : Endpoint<ArcListQuery, ArcListResponse>
{
    private const int MaxSearchLength = 100;

    public override void Configure()
    {
        Get("/communities/{communityId}/architectural-applications");
        Description(x => x.WithName("ListArchitecturalApplications").WithTags("Board"));
    }

    public override async Task HandleAsync(ArcListQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ViewArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var status = (req.Status ?? "open").Trim().ToLowerInvariant();
        if (status is not ("open" or "closed"))
        {
            await ArcHttp.ValidationAsync(HttpContext, "status must be 'open' or 'closed'.", ct);
            return;
        }
        var search = req.Search?.Trim();
        if (search is { Length: > MaxSearchLength })
        {
            await ArcHttp.ValidationAsync(HttpContext, $"search must be at most {MaxSearchLength} characters.", ct);
            return;
        }

        var caller = ArcQueries.CallerId(User);
        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);

        var all = db.ArchitecturalApplications.Where(a => a.CommunityId == req.CommunityId);
        var isBoard = await queries.ActiveBoard(req.CommunityId).AnyAsync(m => m.UserId == caller, ct);
        var awaiting = all.Where(a =>
            a.Status == ArcApplicationStatus.Open
            && !a.Votes.Any(v => v.VoterUserId == caller)
            && !db.UserProperties.Any(up => up.PropertyId == a.PropertyId && up.UserId == caller));

        // Counts ignore search and paging so tab labels and the header pill stay stable (FR-003, FR-005).
        var counts = new ArcCountsDto(
            await all.CountAsync(a => a.Status != ArcApplicationStatus.Closed, ct),
            await all.CountAsync(a => a.Status == ArcApplicationStatus.Closed, ct),
            isBoard ? await awaiting.CountAsync(ct) : 0);

        IQueryable<ArchitecturalApplication> filtered = req.AwaitingMyVote == true
            ? (isBoard ? awaiting : all.Where(_ => false))
            : status == "closed"
                ? all.Where(a => a.Status == ArcApplicationStatus.Closed)
                : all.Where(a => a.Status != ArcApplicationStatus.Closed);

        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            filtered = filtered.Where(a =>
                EF.Functions.ILike(a.Property.Address, pattern, "\\")
                || EF.Functions.ILike(a.OwnerName, pattern, "\\"));
        }

        var ordered = status == "closed" && req.AwaitingMyVote != true
            ? filtered.OrderByDescending(a => a.ClosedAt).ThenByDescending(a => a.ApplicationNumber)
            : filtered.OrderBy(a => a.DueDate).ThenBy(a => a.ApplicationNumber).ThenBy(a => a.Revision);

        var total = await filtered.CountAsync(ct);
        var page = await ordered.Include(a => a.Property).Skip(offset).Take(limit).ToListAsync(ct);
        var items = await queries.BuildItemsAsync(req.CommunityId, page, caller, ct);

        await SendOkAsync(new ArcListResponse(items, total, limit, offset, counts), ct);
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

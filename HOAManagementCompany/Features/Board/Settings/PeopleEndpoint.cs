using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Settings;

public sealed class PeopleQuery
{
    public Guid CommunityId { get; set; }
    public string? Search { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed record PersonItem(string UserId, string Name, IReadOnlyList<string> RolesHere, IReadOnlyList<string> ResidentOf);

/// <summary>
/// GET /communities/{communityId}/people — the people a role can be granted to directly (030
/// FR-018): users with an effective membership or a resident link in this community, without the
/// caller (who can't grant themself a role, FR-003a).
/// </summary>
public class PeopleEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, TimeProvider clock)
    : Endpoint<PeopleQuery, PagedResponse<PersonItem>>
{
    private const int MaxSearch = 100;

    public override void Configure()
    {
        Get("/communities/{communityId}/people");
        Description(x => x.WithName("CommunityPeople").WithTags("Community settings"));
    }

    public override async Task HandleAsync(PeopleQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageMemberships, ScopeOperation.Read, ct), ct))
            return;
        var search = req.Search?.Trim();
        if (search is { Length: > MaxSearch })
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("search", $"Search can be at most {MaxSearch} characters."));

        var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var cid = req.CommunityId;
        var people = db.Users.AsNoTracking().Where(u => u.Id != callerId
            && (db.CommunityMemberships.Any(m => m.UserId == u.Id && m.CommunityId == cid && m.Status == MembershipStatus.Active
                                                && (m.EndDate == null || m.EndDate >= today))
                || db.UserProperties.Any(up => up.UserId == u.Id && up.Property.CommunityId == cid)));
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            // The escape character must be named: Npgsql's two-argument ILike sends ESCAPE ''.
            people = people.Where(u => EF.Functions.ILike(u.FirstName + " " + u.LastName, pattern, "\\"));
        }

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var total = await people.CountAsync(ct);
        var page = await people
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id)
            .Skip(offset).Take(limit)
            .Select(u => new
            {
                u.Id,
                Name = u.FirstName + " " + u.LastName,
                Roles = db.CommunityMemberships
                    .Where(m => m.UserId == u.Id && m.CommunityId == cid && m.Role != CommunityRole.Resident
                                && m.Status == MembershipStatus.Active && (m.EndDate == null || m.EndDate >= today))
                    .Select(m => m.Role).ToList(),
                Homes = db.UserProperties.Where(up => up.UserId == u.Id && up.Property.CommunityId == cid)
                    .Select(up => up.Property.Address).ToList(),
            })
            .ToListAsync(ct);

        var items = page.Select(p => new PersonItem(p.Id, p.Name.Trim(),
            p.Roles.Select(r => r.ToString()).Order().ToList(), p.Homes.Order().ToList())).ToList();
        await SendOkAsync(new PagedResponse<PersonItem>(items, total, limit, offset), ct);
    }
}

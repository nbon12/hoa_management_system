using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// GET /api/v1/me/communities — backs the "My Communities" nav item (FR-025). Returns
// only communities where the caller holds an ACTIVE membership; summary fields only
// (the constitution's intentional cross-community exception). Inherently self-scoped,
// so no resolver gate is needed.
public class MyCommunitiesEndpoint(ApplicationDbContext db)
    : Endpoint<MyCommunitiesQuery, PagedResponse<CommunitySummaryDto>>
{
    public override void Configure()
    {
        Get("/me/communities");
        Description(x => x.WithName("MyCommunities").WithTags("Board"));
    }

    public override async Task HandleAsync(MyCommunitiesQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);

        var rows = await db.CommunityMemberships
            .Where(CommunityMembership.IsEffectiveAsOf(today))
            .Where(m => m.UserId == userId)
            .Select(m => new { m.CommunityId, m.Community.CommunityName, m.Role, m.Community.Status })
            .ToListAsync(ct);

        // One row per community (a user holding two roles in one community appears
        // once, with the active roles joined — Clarifications 2026-08-23).
        var grouped = rows
            .GroupBy(r => new { r.CommunityId, r.CommunityName, r.Status })
            .Select(g => new CommunitySummaryDto(
                g.Key.CommunityId,
                g.Key.CommunityName,
                string.Join("/", g.Select(x => x.Role.ToString()).Distinct().OrderBy(s => s)),
                g.Key.Status.ToString()))
            .OrderBy(c => c.CommunityName)
            .ToList();

        var page = grouped.Skip(offset).Take(limit).ToList();
        await SendOkAsync(new PagedResponse<CommunitySummaryDto>(page, grouped.Count, limit, offset), ct);
    }
}

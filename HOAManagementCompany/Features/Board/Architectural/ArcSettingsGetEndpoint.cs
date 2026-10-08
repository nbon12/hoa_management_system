using System.Text.Json;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// GET /communities/{communityId}/architectural-settings — the community's ARC rules (027 FR-029).
/// Returns defaults without writing when no row exists yet (analyze A2).
/// </summary>
public class ArcSettingsGetEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope)
    : Endpoint<ArcSettingsRoute, ArcSettingsDto>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/architectural-settings");
        Description(x => x.WithName("GetArchitecturalSettings").WithTags("Board"));
    }

    public override async Task HandleAsync(ArcSettingsRoute req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ViewArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var row = await db.CommunityArcSettings.AsNoTracking().FirstOrDefaultAsync(s => s.CommunityId == req.CommunityId, ct);
        await SendOkAsync(ArcSettingsMapping.ToDto(row ?? CommunityArcSettings.Defaults(req.CommunityId), row is not null), ct);
    }
}

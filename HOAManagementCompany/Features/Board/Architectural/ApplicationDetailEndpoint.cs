using FastEndpoints;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// GET /communities/{communityId}/architectural-applications/{applicationId} — the detail panel
/// (027 US3; FR-010–FR-013, FR-019, FR-035). Records a sensitive-access event (025 FR-017).
/// </summary>
public class ApplicationDetailEndpoint(ICommunityScopeResolver scope, ArcQueries queries, ILogger<ApplicationDetailEndpoint> logger)
    : Endpoint<ArcApplicationRoute, ArcDetailDto>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/architectural-applications/{applicationId}");
        Description(x => x.WithName("GetArchitecturalApplication").WithTags("Board"));
    }

    public override async Task HandleAsync(ArcApplicationRoute req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ViewArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var app = await queries.FindInCommunityAsync(req.CommunityId, req.ApplicationId, ct);
        if (app is null)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var caller = ArcQueries.CallerId(User);
        ArcLog.SensitiveAccess(logger, caller, req.CommunityId, $"application:{app.Id}", queries.Now);
        await SendOkAsync(await queries.BuildDetailAsync(app, caller, ct), ct);
    }
}

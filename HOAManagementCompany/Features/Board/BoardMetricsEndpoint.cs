using FastEndpoints;

namespace HOAManagementCompany.Features.Board;

// GET /api/v1/board/metrics — serves the MetricDescriptor registry for a surface
// (User Story 4). Requires an active board-side membership in the community; each
// descriptor is additionally filtered per its RequiredCapability (FR-035). A single
// failing descriptor renders unavailable without blanking its siblings (FR-036). The
// registry ships empty — an empty response is the valid empty state, not an error.
public class BoardMetricsEndpoint(
    ICommunityScopeResolver scope,
    IEnumerable<MetricDescriptor> registry,
    ILogger<BoardMetricsEndpoint> logger)
    : Endpoint<BoardMetricsQuery, PagedResponse<MetricRowDto>>
{
    public override void Configure()
    {
        Get("/board/metrics");
        Description(x => x.WithName("BoardMetrics").WithTags("Board"));
    }

    public override async Task HandleAsync(BoardMetricsQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ViewAssociationData, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        // FR-017: record association-wide (financial) data access — actor, community,
        // resource, UTC timestamp — as a structured sensitive event (constitution §7).
        logger.LogInformation(
            "BoardAssociationDataAccess: Actor={ActorId} Community={CommunityId} Resource={Resource} At={UtcNow:o}",
            BoardHttp.ActorId(User), req.CommunityId, $"board/metrics/{req.Surface}", DateTimeOffset.UtcNow);

        // CommunityCapability is a small fixed enum, so many descriptors on a surface share
        // one required capability. Resolve each capability at most once per request; the gate
        // above already answered ViewAssociationData (it returned 403 when false), so seed it.
        var allowedByCapability = new Dictionary<CommunityCapability, bool>
        {
            [CommunityCapability.ViewAssociationData] = true
        };

        var rows = new List<MetricRowDto>();
        foreach (var d in registry.Where(d => d.Surface == req.Surface))
        {
            // FR-035: silently omit descriptors the caller lacks the capability for.
            // Still evaluated per descriptor — only the repeat DB round trip is avoided.
            if (!allowedByCapability.TryGetValue(d.RequiredCapability, out var allowed))
            {
                allowed = await scope.CanAccessAsync(User, req.CommunityId, d.RequiredCapability, ct);
                allowedByCapability[d.RequiredCapability] = allowed;
            }

            if (!allowed)
                continue;

            MetricValue value;
            try
            {
                value = await d.Resolve(new MetricContext(req.CommunityId));
            }
            catch (Exception ex)
            {
                // FR-036: one failing metric must not blank the page.
                logger.LogWarning(ex, "Metric {MetricId} failed to resolve for community {CommunityId}", d.Id, req.CommunityId);
                value = MetricValue.Unavailable();
            }

            rows.Add(new MetricRowDto(
                d.Id, d.Label, d.DefinitionText, value.Value, value.Detail,
                value.Status.ToString(), d.Emphasis.ToString()));
        }

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var page = rows.Skip(offset).Take(limit).ToList();
        await SendOkAsync(new PagedResponse<MetricRowDto>(page, rows.Count, limit, offset), ct);
    }
}

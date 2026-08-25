namespace HOAManagementCompany.Features.Board;

// The single metric descriptor contract (spec FR-029). Every metric surface —
// hero statistics, metric tables, and the glossary — renders from one collection
// of these, resolved from DI as IEnumerable<MetricDescriptor> (FR-030). Adding or
// retiring a metric is a one-line data change (FR-031, SC-004). Concrete
// descriptors are spec 2's responsibility; this spec ships the type, the registry
// mechanism, and the endpoint, with an empty registry (a valid state per the
// spec's Edge Cases).
public sealed record MetricDescriptor(
    string Id,                                        // stable, e.g. "over-60-days-delinquent"
    MetricSurface Surface,                            // which page/table section renders it
    string Label,
    string DefinitionText,                            // glossary copy — same source as the row (FR-034)
    Func<MetricContext, Task<MetricValue>> Resolve,   // computes value/detail/status per request
    MetricEmphasis Emphasis,
    CommunityCapability RequiredCapability             // FR-035: not rendered if the caller lacks it
);

// Which page/section a descriptor routes to. Sibling specs extend this set.
public enum MetricSurface
{
    CommunityHomeHero,
    CommunityMetrics,
    WorkProcessed
}

public enum MetricEmphasis
{
    Normal,
    Highlight
}

public enum MetricStatus
{
    Ok,
    Watch,
    Unavailable
}

// The resolved value of one metric for one request.
public sealed record MetricValue(string Value, string? Detail, MetricStatus Status)
{
    public static MetricValue Unavailable(string? detail = null) =>
        new("—", detail, MetricStatus.Unavailable);
}

// Context handed to MetricDescriptor.Resolve. Kept minimal; sibling specs extend it
// with the services their concrete metrics need.
public sealed record MetricContext(Guid CommunityId);

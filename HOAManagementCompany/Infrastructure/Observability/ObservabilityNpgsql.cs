// <!-- REPOWISE:START domain=observability -->
// Builds the Npgsql data source with tracing enrichment so DB spans carry the SQL
// statement text as `db.statement` (FR-004). Npgsql 9 does not record command text by
// default; the enrichment callback adds it unconditionally, and the
// TelemetryScrubbingProcessor strips it again when SQL capture is disabled in
// production (FR-010) — keeping the capture decision in one place.
// <!-- REPOWISE:END -->

using Npgsql;

namespace HOAManagementCompany.Infrastructure.Observability;

public static class ObservabilityNpgsql
{
    /// <summary>
    /// Creates a pooled <see cref="NpgsqlDataSource"/> whose command spans include the
    /// SQL text. Register the result as a singleton so it is disposed with the container.
    /// </summary>
    public static NpgsqlDataSource BuildTracedDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConfigureTracing(tracing => tracing
            .ConfigureCommandEnrichmentCallback((activity, command) =>
                activity.SetTag("db.statement", command.CommandText)));
        return builder.Build();
    }
}

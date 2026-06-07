// <!-- REPOWISE:START domain=observability -->
// Single owner of the OpenTelemetry pipeline (FR-023). Registers the resource
// (service.name), the OTLP/HTTP-protobuf exporter pointed at the env-configured
// destination (FR-007/FR-015/FR-022), a head-based sampler (FR-027), the sensitive-data
// scrubbing processor (FR-009), and — added by their respective user stories — ASP.NET
// Core + HttpClient + Npgsql instrumentation and the metrics pipeline. The OTLP exporter
// is skipped in the Test environment, where in-memory exporters are appended instead.
// <!-- REPOWISE:END -->

using HOAManagementCompany.Infrastructure.Observability;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HOAManagementCompany.Infrastructure.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenTelemetry tracing + metrics pipeline and the shared scrubbing
    /// policy. Telemetry-init must never block startup; the caller guards this (FR-008).
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        // Snapshot used only for build-time pipeline settings (resource, sampler, exporter
        // endpoint) — these come from OTEL_*/appsettings, available before the host is built.
        var startup = ObservabilityOptions.FromConfiguration(builder.Configuration, builder.Environment);

        // Runtime-bound options/policy: resolved from the FINAL IConfiguration so per-request
        // settings (e.g. CaptureSqlText) reflect overrides applied during host build. The
        // telemetry proxy and the scrubbing processor consume these (FR-007/FR-010).
        builder.Services.AddSingleton(sp => ObservabilityOptions.FromConfiguration(
            sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>()));
        builder.Services.AddSingleton(sp => new ScrubbingPolicy(
            sp.GetRequiredService<ObservabilityOptions>().ScrubbedKeys));

        var exportToOtlp = !builder.Environment.IsEnvironment("Test");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: startup.ServiceName))
            .WithTracing(tracing =>
            {
                // Head-based, parent-respecting sampler (default 100%, FR-027).
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(startup.TraceSampleRatio)));

                // Source-level scrubbing before anything leaves the process (FR-009). Built from
                // runtime options so the SQL-text gate (FR-010) honors the resolved config.
                tracing.AddProcessor(sp => new TelemetryScrubbingProcessor(
                    sp.GetRequiredService<ScrubbingPolicy>(),
                    sp.GetRequiredService<ObservabilityOptions>().CaptureSqlText));

                // Instrumentations are added by their user stories:
                //   US1 → AddAspNetCoreInstrumentation / AddHttpClientInstrumentation
                //   US4 → AddNpgsql
                AddTracingInstrumentation(tracing, startup);

                if (exportToOtlp)
                    tracing.AddOtlpExporter(o => ConfigureOtlp(o, startup));
            })
            .WithMetrics(metrics =>
            {
                AddMetricsInstrumentation(metrics);

                if (exportToOtlp)
                    metrics.AddOtlpExporter(o => ConfigureOtlp(o, startup));
            });

        return builder;
    }

    // Applies the env-configured OTLP destination uniformly to every signal (FR-007).
    private static void ConfigureOtlp(OtlpExporterOptions o, ObservabilityOptions options)
    {
        o.Endpoint = new Uri(options.OtlpEndpoint);
        o.Protocol = OtlpExportProtocol.HttpProtobuf; // FR-022: HTTP/protobuf only.
        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            o.Headers = options.OtlpHeaders;
    }

    // Tracing instrumentation.
    private static void AddTracingInstrumentation(TracerProviderBuilder tracing, ObservabilityOptions options)
    {
        // US1: inbound HTTP server spans + outbound HttpClient spans.
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();

        // US4: PostgreSQL spans via Npgsql's built-in OTel source (FR-004).
        tracing.AddNpgsql();
    }

    // Metrics instrumentation — ASP.NET Core + HttpClient meters give request count,
    // duration histogram, and error rate per endpoint (FR-012).
    private static void AddMetricsInstrumentation(MeterProviderBuilder metrics)
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();

        // US3: custom payment-domain counters (payment.processed / webhook.processed / alert.sent).
        metrics.AddMeter(PaymentMetrics.MeterName);
    }
}

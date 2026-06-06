// <!-- REPOWISE:START domain=observability -->
// Strongly-typed observability configuration bound from `Observability__*` and the
// standard `OTEL_*` environment variables. Drives the OpenTelemetry pipeline, the
// Serilog OTLP sink, Sentry-on-OTel sampling, SQL-text gating, scrubbing, and the
// browser telemetry proxy. No code change is required to switch environments (FR-007).
// <!-- REPOWISE:END -->

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HOAManagementCompany.Infrastructure.Observability;

/// <summary>
/// Runtime observability settings. Sourced from configuration so the telemetry
/// destination, sampling, and scrubbing can be changed by environment variable only.
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>OTLP/HTTP receiver for backend traces, logs, and metrics (FR-007/FR-015).</summary>
    public string OtlpEndpoint { get; set; } = "http://aspire-dashboard:18890";

    /// <summary>OTLP transport. Only <c>http/protobuf</c> is supported (FR-022).</summary>
    public string OtlpProtocol { get; set; } = "http/protobuf";

    /// <summary>Optional vendor auth headers, e.g. <c>Authorization=Bearer xxx</c> (FR-029).</summary>
    public string? OtlpHeaders { get; set; }

    /// <summary>Logical service name reported on every signal (FR-007).</summary>
    public string ServiceName { get; set; } = "hoa-api";

    /// <summary>Head-based OTel trace sampling ratio (FR-027). Default 100%.</summary>
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>Sentry's independent trace sample rate, decoupled from OTel (FR-023).</summary>
    public double SentryTraceSampleRatio { get; set; } = 0.2;

    /// <summary>Whether DB spans carry the full SQL text. On in Dev, off in Prod (FR-004/FR-010).</summary>
    public bool CaptureSqlText { get; set; } = true;

    /// <summary>Maximum accepted browser telemetry body size in bytes (FR-031). Default 1 MiB.</summary>
    public int TelemetryProxyMaxBodyBytes { get; set; } = 1_048_576;

    /// <summary>Attribute/field keys whose values are redacted before export (FR-009).</summary>
    public string[] ScrubbedKeys { get; set; } =
    {
        "password", "token", "cardNumber", "cardCvv",
        "routingNumber", "accountNumber", "email", "fullName"
    };

    /// <summary>Optional structured-log file sink path (tests/diagnostics, FR-021).</summary>
    public string? LogFilePath { get; set; }

    /// <summary>Optional log file rotation interval hint (e.g. <c>Day</c>).</summary>
    public string? LogRotation { get; set; }

    /// <summary>True when an explicit OTLP endpoint was configured via environment/config.</summary>
    public bool HasExplicitEndpoint { get; private set; }

    /// <summary>
    /// Binds the <c>Observability</c> config section and overlays the standard
    /// <c>OTEL_*</c> environment variables (which take precedence). Applies the
    /// environment-specific defaults for SQL capture (on in Dev, off otherwise).
    /// </summary>
    public static ObservabilityOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var options = new ObservabilityOptions();
        configuration.GetSection(SectionName).Bind(options);

        // SQL text defaults to ON in Development, OFF elsewhere, unless explicitly set.
        if (configuration[$"{SectionName}:CaptureSqlText"] is null)
            options.CaptureSqlText = environment.IsDevelopment();

        // Standard OTEL_* env vars win over section defaults so operators can repoint
        // telemetry with the canonical OpenTelemetry knobs (FR-007).
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            options.OtlpEndpoint = endpoint;
            options.HasExplicitEndpoint = true;
        }

        var protocol = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        if (!string.IsNullOrWhiteSpace(protocol))
            options.OtlpProtocol = protocol;

        var headers = configuration["OTEL_EXPORTER_OTLP_HEADERS"];
        if (!string.IsNullOrWhiteSpace(headers))
            options.OtlpHeaders = headers;

        var serviceName = configuration["OTEL_SERVICE_NAME"];
        if (!string.IsNullOrWhiteSpace(serviceName))
            options.ServiceName = serviceName;

        return options;
    }
}

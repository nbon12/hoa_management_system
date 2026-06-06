// <!-- REPOWISE:START domain=observability -->
// Source-level sensitive-data scrubbing (FR-009). A single policy redacts span
// attributes (OTel BaseProcessor<Activity>) and structured-log fields (Serilog
// enricher) whose key matches the configured set (passwords, tokens, card / account /
// routing numbers, emails, full names) before anything is exported. The same processor
// optionally strips DB statement text when SQL capture is disabled (FR-010).
// <!-- REPOWISE:END -->

using System.Diagnostics;
using OpenTelemetry;
using Serilog.Core;
using Serilog.Events;

namespace HOAManagementCompany.Infrastructure.Observability;

/// <summary>
/// Shared redaction policy used by both the trace processor and the log enricher so
/// the scrubbed field set is defined in exactly one place (FR-009).
/// </summary>
public sealed class ScrubbingPolicy
{
    public const string Redacted = "[REDACTED]";

    /// <summary>Span/log attribute keys that carry SQL statement text.</summary>
    private static readonly string[] SqlStatementKeys = { "db.statement", "db.query.text" };

    private readonly string[] _normalizedKeys;

    public ScrubbingPolicy(IEnumerable<string> scrubbedKeys)
    {
        _normalizedKeys = scrubbedKeys
            .Select(Normalize)
            .Where(k => k.Length > 0)
            .Distinct()
            .ToArray();
    }

    /// <summary>True if the attribute/field key should have its value redacted.</summary>
    public bool IsSensitive(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var normalized = Normalize(key);
        foreach (var sensitive in _normalizedKeys)
            if (normalized.Contains(sensitive, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>True if the key holds SQL statement text (used when SQL capture is off).</summary>
    public static bool IsSqlStatementKey(string key) =>
        Array.Exists(SqlStatementKeys, k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    // Lowercase and drop non-alphanumerics so "card.number", "CardNumber", and
    // "card_number" all collapse to the same comparable token.
    private static string Normalize(string value)
    {
        Span<char> buffer = value.Length <= 128 ? stackalloc char[value.Length] : new char[value.Length];
        var len = 0;
        foreach (var c in value)
            if (char.IsLetterOrDigit(c))
                buffer[len++] = char.ToLowerInvariant(c);
        return new string(buffer[..len]);
    }
}

/// <summary>
/// OTel span processor that redacts sensitive tag values and, when SQL capture is
/// disabled, strips DB statement text before the span is exported (FR-009/FR-010).
/// </summary>
public sealed class TelemetryScrubbingProcessor : BaseProcessor<Activity>
{
    private readonly ScrubbingPolicy _policy;
    private readonly bool _captureSqlText;

    public TelemetryScrubbingProcessor(ScrubbingPolicy policy, bool captureSqlText)
    {
        _policy = policy;
        _captureSqlText = captureSqlText;
    }

    public override void OnEnd(Activity activity)
    {
        // Materialize first: SetTag mutates the underlying collection we are iterating.
        List<KeyValuePair<string, object?>>? updates = null;

        foreach (var tag in activity.TagObjects)
        {
            var stripSql = !_captureSqlText && ScrubbingPolicy.IsSqlStatementKey(tag.Key);
            if (stripSql)
            {
                (updates ??= new()).Add(new(tag.Key, null));
                continue;
            }

            if (tag.Value is not null && _policy.IsSensitive(tag.Key))
                (updates ??= new()).Add(new(tag.Key, ScrubbingPolicy.Redacted));
        }

        if (updates is null) return;
        foreach (var update in updates)
            activity.SetTag(update.Key, update.Value); // null value removes the tag
    }
}

/// <summary>
/// Serilog enricher that redacts log properties whose name matches the scrubbed key
/// set. Backend logs flow Serilog → OTLP sink (not the OTel LogRecord pipeline), so the
/// log-side scrub lives here (FR-009).
/// </summary>
public sealed class TelemetryScrubbingEnricher : ILogEventEnricher
{
    private readonly ScrubbingPolicy _policy;

    public TelemetryScrubbingEnricher(ScrubbingPolicy policy) => _policy = policy;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties)
            if (_policy.IsSensitive(property.Key))
                logEvent.AddOrUpdateProperty(new LogEventProperty(
                    property.Key, new ScalarValue(ScrubbingPolicy.Redacted)));
    }
}

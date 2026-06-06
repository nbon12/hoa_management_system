using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// A real Serilog sink (no test doubles, SC-008) that captures emitted log events in
/// memory for integration assertions. Each event is also rendered to compact JSON so
/// tests can verify structured-log validity and field presence (FR-018/FR-019/SC-009).
/// Injected into the host via DI and composed by Serilog's <c>ReadFrom.Services</c>.
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();
    private readonly ConcurrentQueue<string> _json = new();
    private readonly CompactJsonFormatter _formatter = new();

    public void Emit(LogEvent logEvent)
    {
        _events.Enqueue(logEvent);

        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        _json.Enqueue(writer.ToString().TrimEnd('\r', '\n'));
    }

    /// <summary>Captured events for property-level assertions (trace_id, user_id, …).</summary>
    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    /// <summary>Captured events rendered as compact JSON lines (one document per entry).</summary>
    public IReadOnlyList<string> JsonLines => _json.ToArray();
}

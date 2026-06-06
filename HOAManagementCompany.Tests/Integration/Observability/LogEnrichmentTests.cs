using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US3 / FR-003 / FR-011: every log entry from an in-flight request carries the trace_id
/// and span_id, and an authenticated request's entries carry the user's subject GUID —
/// never the email/username.
/// </summary>
public class LogEnrichmentTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Email = "resident@nekohoa.dev";
    private const string Password = "Password1!";

    private async Task<string> LoginAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["token"]!.ToString()!;
    }

    private static string? Prop(LogEvent e, string key) =>
        e.Properties.TryGetValue(key, out var v) && v is ScalarValue sv ? sv.Value?.ToString() : null;

    /// <summary>Decodes the JWT 'sub' claim — the subject identifier we expect in logs.</summary>
    private static string SubjectFromJwt(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return doc.RootElement.GetProperty("sub").GetString()!;
    }

    [Fact]
    public async Task InFlightRequest_AllLogEntries_CarryTraceIdAndSpanId()
    {
        var token = await LoginAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var before = LogSink.Events.Count;
        await Client.GetAsync("/api/v1/auth/me");
        FlushTelemetry();

        var newEvents = LogSink.Events.Skip(before).ToList();
        Assert.NotEmpty(newEvents);

        // 100% of in-flight log entries carry trace and span IDs (SC-003).
        Assert.All(newEvents, e =>
        {
            Assert.False(string.IsNullOrEmpty(Prop(e, "trace_id")), "log entry missing trace_id");
            Assert.False(string.IsNullOrEmpty(Prop(e, "span_id")), "log entry missing span_id");
        });

        // The trace_id matches the most recent (the /me) request's server span.
        var serverSpan = ExportedSpans.Last(s => s.Kind == ActivityKind.Server);
        Assert.Contains(newEvents, e => Prop(e, "trace_id") == serverSpan.TraceId.ToHexString());
    }

    [Fact]
    public async Task AuthenticatedRequest_LogEntries_CarryUserGuid_NotEmail()
    {
        var token = await LoginAsync();
        var expectedSubject = SubjectFromJwt(token);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var before = LogSink.Events.Count;
        await Client.GetAsync("/api/v1/auth/me");

        var newEvents = LogSink.Events.Skip(before).ToList();

        // user_id is present and is the subject identifier, not the email/username (FR-011).
        var userIds = newEvents
            .Select(e => Prop(e, "user_id"))
            .Where(v => v is not null)
            .ToList();
        Assert.NotEmpty(userIds);
        Assert.All(userIds, v => Assert.Equal(expectedSubject, v));
        Assert.DoesNotContain("@", expectedSubject);

        // No log property value may leak the email/username.
        foreach (var e in newEvents)
            foreach (var p in e.Properties)
                Assert.DoesNotContain("@nekohoa.dev", p.Value.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

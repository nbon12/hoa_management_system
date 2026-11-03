using System.Net.Http.Json;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

// 019-C FR-C1: the Serilog scrubbing enricher must be registered and must redact sensitive
// fields (e.g. {Email}) in every emitted log event — it was written and tested but never wired in.
public class LogScrubbingTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static string? Prop(LogEvent e, string key) =>
        e.Properties.TryGetValue(key, out var v) && v is ScalarValue sv ? sv.Value?.ToString() : null;

    [Fact]
    public void ScrubbingEnricher_IsRegistered()
    {
        var enrichers = Services.GetServices<ILogEventEnricher>();
        Assert.Contains(enrichers, e => e is TelemetryScrubbingEnricher);
    }

    [Fact]
    public async Task FailedLogin_EmailProperty_IsRedactedInEmittedLogs()
    {
        const string email = "scrub-probe@nekohoa.dev";
        var before = LogSink.Events.Count;

        // A failed login logs "Failed login attempt for {Email}".
        await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "wrong-password" });
        FlushTelemetry();

        var events = LogSink.Events.Skip(before).ToList();
        var withEmail = events.Where(e => e.Properties.ContainsKey("Email")).ToList();

        Assert.NotEmpty(withEmail);
        Assert.All(withEmail, e => Assert.Equal(ScrubbingPolicy.Redacted, Prop(e, "Email")));

        // No property value anywhere may leak the raw email.
        foreach (var e in events)
            foreach (var p in e.Properties)
                Assert.DoesNotContain(email, p.Value.ToString(), System.StringComparison.OrdinalIgnoreCase);
    }
}

using System.Diagnostics;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US1 / FR-012: a backend HTTP request must produce a server span carrying the standard
/// HTTP semantic attributes, exported to the in-memory trace exporter.
/// </summary>
public class BackendTracingTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task BackendRequest_ProducesServerSpan_WithHttpMethodAndRoute()
    {
        await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });

        FlushTelemetry();

        var serverSpan = ExportedSpans.FirstOrDefault(s =>
            s.Kind == ActivityKind.Server &&
            s.TagObjects.Any(t => t.Key == "http.request.method"));

        Assert.NotNull(serverSpan);

        var method = serverSpan!.TagObjects.First(t => t.Key == "http.request.method").Value;
        Assert.Equal("POST", method);

        // A route or path must be present so the span is attributable to an endpoint.
        Assert.Contains(serverSpan.TagObjects, t =>
            t.Key is "http.route" or "url.path" && t.Value is string s && s.Contains("login"));
    }
}

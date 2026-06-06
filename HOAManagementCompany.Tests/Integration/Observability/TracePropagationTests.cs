using System.Diagnostics;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US2 / FR-002: a request carrying an inbound W3C <c>traceparent</c> continues the same
/// trace on the backend server span, so a browser-originated trace links end to end.
/// </summary>
public class TracePropagationTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task InboundTraceparent_ContinuesSameTraceId_OnServerSpan()
    {
        // A valid W3C traceparent: version-traceid(32 hex)-spanid(16 hex)-flags.
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        const string parentSpanId = "b7ad6b7169203331";
        var traceparent = $"00-{traceId}-{parentSpanId}-01";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "resident@nekohoa.dev", password = "Password1!" })
        };
        request.Headers.Add("traceparent", traceparent);

        await Client.SendAsync(request);
        FlushTelemetry();

        var serverSpan = ExportedSpans.FirstOrDefault(s => s.Kind == ActivityKind.Server);
        Assert.NotNull(serverSpan);
        Assert.Equal(traceId, serverSpan!.TraceId.ToHexString());
        Assert.Equal(parentSpanId, serverSpan.ParentSpanId.ToHexString());
    }
}

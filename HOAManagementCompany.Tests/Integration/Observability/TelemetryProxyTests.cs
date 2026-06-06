using System.Net;
using System.Net.Http.Headers;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US2 / FR-031 (contracts/telemetry-proxy.openapi.yaml): the browser telemetry proxy
/// accepts anonymous OTLP protobuf (202), enforces the body cap (413), media type (415),
/// and the per-client rate limit (429). The destination is server-configured and can
/// never be specified by the client (FR-029).
/// </summary>
public class TelemetryProxyTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Url = "/api/v1/telemetry";

    private readonly CapturingHandler _forward = new();

    // Server-configured vendor destination + credential (US5/FR-029).
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new[]
    {
        new KeyValuePair<string, string?>("OTEL_EXPORTER_OTLP_ENDPOINT", "https://vendor.example.com"),
        new KeyValuePair<string, string?>("OTEL_EXPORTER_OTLP_HEADERS", "Authorization=Bearer secret-vendor-token"),
    };

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddHttpClient("telemetry-forward").ConfigurePrimaryHttpMessageHandler(() => _forward);

    private static ByteArrayContent Protobuf(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        return content;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? ForwardedUri;
        public string? AuthorizationHeader;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ForwardedUri = request.RequestUri?.ToString();
            AuthorizationHeader = request.Headers.TryGetValues("Authorization", out var v) ? string.Join(",", v) : null;
            Done.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task Post_AnonymousProtobuf_Returns202()
    {
        // A small opaque OTLP payload; forwarding is fire-and-forget and swallowed.
        var response = await Client.PostAsync(Url, Protobuf(new byte[] { 0x0a, 0x00 }));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_OverBodyCap_Returns413()
    {
        // Default cap is 1 MiB; send well over it.
        var oversized = new byte[1_200_000];
        var response = await Client.PostAsync(Url, Protobuf(oversized));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Post_WrongMediaType_Returns415()
    {
        var content = new ByteArrayContent(new byte[] { 0x01 });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await Client.PostAsync(Url, content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExceedsRateLimit_Returns429()
    {
        // The harness configures a permit limit of 5 per window.
        HttpStatusCode? rejected = null;
        for (var i = 0; i < 10; i++)
        {
            var response = await Client.PostAsync(Url, Protobuf(new byte[] { 0x0a, 0x00 }));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response.StatusCode;
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected);
    }

    [Fact]
    public async Task Post_ForwardsToServerConfiguredDestination_WithVendorCredential_IgnoringClient()
    {
        var content = Protobuf(new byte[] { 0x0a, 0x00 });
        // A malicious client cannot redirect the forward — destination is server-only (FR-029).
        content.Headers.TryAddWithoutValidation("X-Client-Destination", "https://evil.example.com");

        var response = await Client.PostAsync(Url, content);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // Forwarding is fire-and-forget; wait for the captured request.
        await Task.WhenAny(_forward.Done.Task, Task.Delay(5000));
        Assert.True(_forward.Done.Task.IsCompleted, "proxy did not forward the payload");

        Assert.Equal("https://vendor.example.com/v1/traces", _forward.ForwardedUri);
        Assert.Equal("Bearer secret-vendor-token", _forward.AuthorizationHeader);
    }
}

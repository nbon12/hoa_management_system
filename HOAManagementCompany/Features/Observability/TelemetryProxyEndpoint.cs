// <!-- REPOWISE:START domain=observability -->
// Browser telemetry egress proxy (FR-016/FR-029/FR-031). Anonymous, rate-limited, and
// body-size-capped. Accepts OTLP/HTTP protobuf trace payloads from the browser, then
// forwards them PASSTHROUGH (preserving the browser's trace/span IDs for end-to-end
// correlation) to the SERVER-configured destination — never a client-specified one —
// attaching vendor credentials server-side. Fire-and-forget: forwarding failures are
// swallowed and never surfaced to the client (FR-008). The destination's own ingest
// performs final scrubbing; browser spans are emitted traces-only from controlled
// document-load + XHR instrumentation that carries no PII attributes (FR-009).
// <!-- REPOWISE:END -->

using System.Buffers;
using System.Net.Http.Headers;
using FastEndpoints;
using HOAManagementCompany.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;

namespace HOAManagementCompany.Features.Observability;

public class TelemetryProxyEndpoint(IHttpClientFactory httpClientFactory, ObservabilityOptions options)
    : EndpointWithoutRequest
{
    private const string ProtobufMediaType = "application/x-protobuf";

    public override void Configure()
    {
        Post("/telemetry");
        AllowAnonymous(); // pre-login pages must still emit traces (FR-031).
        Description(x => x
            .WithName("IngestBrowserTelemetry")
            .WithTags("Observability")
            .RequireRateLimiting("telemetry"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var request = HttpContext.Request;

        // 415 — only OTLP/HTTP protobuf is accepted; gRPC/JSON are rejected (FR-022).
        if (request.ContentType is not { } contentType ||
            !contentType.StartsWith(ProtobufMediaType, StringComparison.OrdinalIgnoreCase))
        {
            await SendResultAsync(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));
            return;
        }

        // 413 — reject oversize payloads up front when Content-Length is known (FR-031).
        if (request.ContentLength is long declared && declared > options.TelemetryProxyMaxBodyBytes)
        {
            await SendResultAsync(Results.StatusCode(StatusCodes.Status413PayloadTooLarge));
            return;
        }

        var payload = await ReadCappedBodyAsync(request.Body, options.TelemetryProxyMaxBodyBytes, ct);
        if (payload is null)
        {
            // Cap exceeded while streaming (chunked/unknown length).
            await SendResultAsync(Results.StatusCode(StatusCodes.Status413PayloadTooLarge));
            return;
        }

        // Forward without awaiting — telemetry must never delay or fail the client (FR-008).
        _ = ForwardAsync(payload);

        await SendResultAsync(Results.StatusCode(StatusCodes.Status202Accepted));
    }

    /// <summary>Reads the body enforcing the byte cap; returns null if the cap is exceeded.</summary>
    private static async Task<byte[]?> ReadCappedBodyAsync(Stream body, int maxBytes, CancellationToken ct)
    {
        using var buffered = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int read;
            while ((read = await body.ReadAsync(rented, ct)) > 0)
            {
                if (buffered.Length + read > maxBytes)
                    return null;
                buffered.Write(rented, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return buffered.ToArray();
    }

    private async Task ForwardAsync(byte[] payload)
    {
        try
        {
            var client = httpClientFactory.CreateClient("telemetry-forward");
            var destination = $"{options.OtlpEndpoint.TrimEnd('/')}/v1/traces";

            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufMediaType);

            using var message = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, destination) { Content = content };

            // Vendor credentials are attached SERVER-SIDE only (FR-029).
            if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
                foreach (var pair in options.OtlpHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var idx = pair.IndexOf('=');
                    if (idx > 0)
                        message.Headers.TryAddWithoutValidation(pair[..idx].Trim(), pair[(idx + 1)..].Trim());
                }

            await client.SendAsync(message);
        }
        catch
        {
            // Swallow: forwarding failures are never surfaced to the client (FR-008).
        }
    }
}

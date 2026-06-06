# Contracts: Full-Stack Observability

This feature introduces **one** new external interface and **no** changes to existing contracts.

## 1. Telemetry Proxy endpoint — `POST /api/v1/telemetry`

See [`telemetry-proxy.openapi.yaml`](./telemetry-proxy.openapi.yaml). This is the **only**
HTTP contract added. Key contract facts (constitution-aligned):

- **Method/path**: `POST /api/v1/telemetry` (FastEndpoints, not MVC).
- **Auth**: anonymous permitted by design (captures pre-login traces); rate-limited.
- **Body**: OTLP/HTTP **protobuf** (`application/x-protobuf`), ≤ configured cap (default 1 MiB).
- **Responses**: `202` accept (no body), `413` too large, `415` wrong media type, `429` throttled.
- **Not a collection** → pagination N/A. **Not cacheable / not edge-cached** (POST).
- **No response envelope** (status-only); trace IDs never appear in any response body.
- **Destination** is server-configured (env vars); the client can never choose it (no open relay).

## 2. Outbound OTLP contracts (not HTTP endpoints we expose, documented for completeness)

These are **client** behaviors of our services toward telemetry destinations:

- **Backend → destination**: OTLP/HTTP protobuf to `OTEL_EXPORTER_OTLP_ENDPOINT`
  (`/v1/traces`, `/v1/logs`, `/v1/metrics`). Aspire Dashboard HTTP receiver = port **18890**.
- **Frontend → proxy**: OTLP/HTTP protobuf traces to `telemetryUrl` (the endpoint above).
- **Proxy → destination**: passthrough of the frontend OTLP payload + server-side credentials.

## 3. Trace-context propagation contract (frontend ↔ backend)

- Frontend injects the W3C **`traceparent`** header on outbound API calls
  (`propagateTraceHeaderCorsUrls` set to the API origin for cross-origin cases).
- Backend continues the trace from `traceparent` (default W3C propagator).
- The backend CORS policy MUST allow `traceparent`/`tracestate` request headers and the
  `application/x-protobuf` content type on the telemetry endpoint.

## Verification
- Endpoint behavior → `HOAManagementCompany.Tests/Integration/Observability/TelemetryProxyTests.cs`
  (passthrough, anonymous, 413 size cap, 429 rate limit, no-destination-injection).
- Propagation → trace-id continuity asserted via OTel in-memory exporter (FR-002).
- Frontend init + `traceparent` presence → `otel.bootstrap.spec.ts` (Jasmine/Karma).

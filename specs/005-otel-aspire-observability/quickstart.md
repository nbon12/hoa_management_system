# Quickstart: Full-Stack Observability

**Feature**: 005-otel-aspire-observability

Goal: from a clean checkout, run the stack and see a single end-to-end trace (browser → API →
database) in the local dashboard within ~5 minutes (SC-007), with no accounts or API keys (FR-006).

## Prerequisites
- Docker + Docker Compose
- (For tests) .NET 9 SDK and Node 20+ / Angular CLI

## 1. Start the stack

```bash
docker compose up --build
```

This brings up the existing services (`db`, `minio`, `api`, `ui`) plus the new
**`aspire-dashboard`** service.

## 2. Open the dashboard

- Aspire Dashboard UI: **http://localhost:18888** (no login — anonymous local mode, FR-006).
- App UI: http://localhost:4200

The dashboard receives OTLP/HTTP on port **18890**; the API and the telemetry proxy export to it.

## 3. Generate a trace

1. In the app (http://localhost:4200), perform an action that calls the API
   (e.g., load the resident dashboard, or submit a payment).
2. In the Aspire Dashboard → **Traces**, within ~30 s (SC-002) you should see one trace with:
   - a **browser** span (document-load / XHR), and
   - the **API handler** span, and
   - one or more **database** spans showing the SQL text (dev only, FR-004),
   all sharing one `trace_id` (FR-002).
3. Dashboard → **Structured logs**: entries for that request carry the same `trace_id`/`span_id`
   and the user's GUID (FR-003/FR-011). Dashboard → **Metrics**: request count/duration/error rate
   per endpoint (FR-012).

## 4. Verify failure-resilience (optional)

```bash
docker compose stop aspire-dashboard
# hit the API — it must keep serving normally; telemetry is dropped, not buffered (FR-008)
docker compose start aspire-dashboard
# new telemetry reappears within ~30 s (buffered backlog is not guaranteed)
```

## 5. Switch to a production vendor (optional, FR-007/SC-004)

Set ≤3 env vars on the `api` service and restart — no code change:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://<vendor-otlp-endpoint>
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer <token>
OTEL_SERVICE_NAME=hoa-api
```

The same browser/API telemetry now appears in the vendor's dashboard. The browser still posts to
`/api/v1/telemetry`; the proxy attaches the vendor credential server-side (FR-016/FR-029).

## 6. Run the tests (no telemetry container needed)

```bash
# Backend integration tests (in-process WebApplicationFactory + Testcontainers)
dotnet test HOAManagementCompany.Tests
# Asserts: log entries carry trace/span/user-id (Serilog sink); SQL text in spans (in-memory
# exporter); telemetry proxy passthrough/anon/size-cap/rate-limit; scrubbing (Theory).

# Frontend OTel init unit test
cd neko-hoa && npm test
# Asserts: provider initializes without throwing; traceparent present on outbound XHR.
```

No Aspire Dashboard or collector container is started by the test suite (FR-024/FR-025).

## Key environment variables (reference)

| Variable | Purpose | Dev default |
|----------|---------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Telemetry destination | `http://aspire-dashboard:18890` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Transport | `http/protobuf` |
| `OTEL_EXPORTER_OTLP_HEADERS` | Vendor auth (prod) | (unset) |
| `OTEL_SERVICE_NAME` | Service name | `hoa-api` |
| `Observability__TraceSampleRatio` | OTel sampling | `1.0` |
| `Observability__SentryTraceSampleRatio` | Sentry sampling | `0.2` |
| `Observability__CaptureSqlText` | SQL text in spans | `true` (dev) / `false` (prod) |
| `Observability__TelemetryProxyMaxBodyBytes` | Proxy body cap | `1048576` |

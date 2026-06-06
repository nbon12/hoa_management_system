# Phase 1 Data Model: Full-Stack Observability

**Feature**: 005-otel-aspire-observability | **Date**: 2026-06-05

> **No database schema changes.** This feature introduces **no new PostgreSQL tables, columns,
> or migrations.** The "entities" below are runtime/telemetry signal shapes (OpenTelemetry data
> model) and configuration objects — not persisted domain entities. They are documented here so
> contracts, tests, and instrumentation share one vocabulary.

---

## Telemetry signal entities (runtime, not persisted)

### Trace
A single user-initiated operation from browser action to database response.
- **trace_id** (16-byte W3C ID, hex): shared across all spans of the operation.
- **spans**: ordered set of Spans linked by `trace_id`.
- **Origin**: created in the browser (document-load or XHR span) and continued by the backend via
  the `traceparent` header (FR-001/FR-002).
- **Invariants**: one `trace_id` spans frontend + backend + DB; sampling decision is head-based
  and propagated (FR-027).

### Span
An individual unit of work within a Trace.
- **span_id** (8-byte hex), **parent_span_id** (nullable), **trace_id**.
- **name**, **kind** (server | client | internal | producer | consumer).
- **start_time**, **duration**.
- **status** (unset | ok | error) with optional error detail (FR — US2 #2).
- **attributes** (key/value): e.g., `http.request.method`, `url.path`, `http.response.status_code`;
  for DB spans `db.system`, and `db.statement` **only when SQL capture is enabled** (FR-004/FR-010).
- **Invariants**: scrubbed attributes never contain PII/financial/credentials (FR-009); no
  HOA/association/community identifiers in shared-dashboard trace attributes (Tenant boundary).

### Log Entry
A structured, machine-readable event emitted during request handling.
- **timestamp** (UTC), **severity**, **message** (template + rendered).
- **trace_id**, **span_id** (present for any entry during an in-flight request — FR-003/SC-003).
- **user_id**: authenticated user's **subject GUID** (`NameIdentifier`) — never email/PII (FR-011).
- **environment**, plus enriched fields.
- **Format**: structured JSON (FR-018/FR-019); valid JSON with the minimum field set (SC-009).
- **Invariants**: scrubbed of sensitive fields (FR-009); user_id is the GUID, not the username.

### Metric
A numeric measurement aggregated over time (backend only).
- **name** (e.g., `http.server.request.duration`, request count, error rate).
- **type** (counter | histogram | gauge), **unit**, **data points**, **attributes** (route, status).
- **Coverage**: at minimum request count, duration distribution, error rate per endpoint (FR-012).
- **Invariants**: emitted by the .NET backend only — the frontend emits **no** metrics (FR-005/FR-030).

---

## Configuration objects (bound from environment)

### ObservabilityOptions (backend)
Bound from configuration/env; drives instrumentation. No code change to switch environments (FR-007).

| Field | Env / config key | Default (Dev) | Default (Prod) | Requirement |
|-------|------------------|---------------|----------------|-------------|
| `OtlpEndpoint` | `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://aspire-dashboard:18890` | vendor URL | FR-007/FR-015 |
| `OtlpProtocol` | `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` | `http/protobuf` | FR-022 |
| `OtlpHeaders` | `OTEL_EXPORTER_OTLP_HEADERS` | (none) | vendor auth header | FR-007/FR-029 |
| `ServiceName` | `OTEL_SERVICE_NAME` | `hoa-api` | `hoa-api` | FR-007 |
| `TraceSampleRatio` | `Observability__TraceSampleRatio` | `1.0` | `1.0` | FR-027 |
| `SentryTraceSampleRatio` | `Observability__SentryTraceSampleRatio` | `0.2` | `0.2` | FR-023 |
| `CaptureSqlText` | `Observability__CaptureSqlText` | `true` | `false` | FR-004/FR-010 |
| `TelemetryProxyMaxBodyBytes` | `Observability__TelemetryProxyMaxBodyBytes` | `1048576` | `1048576` | FR-031 |
| `ScrubbedKeys` | `Observability__ScrubbedKeys` | password, token, cardNumber, cardCvv, routingNumber, accountNumber, email, fullName | same | FR-009 |
| `LogFileSink` (test/optional) | `Observability__LogFilePath`, `__LogRotation` | (unset) | (unset) | FR-021 |

### Frontend telemetry config (`environment.*.ts`)

| Field | `environment.docker.ts` | `environment.development.ts` | `environment.ts` (prod) | Requirement |
|-------|--------------------------|------------------------------|--------------------------|-------------|
| `telemetryUrl` | `/api/v1/telemetry` (same-origin via nginx) | `http://localhost:5212/api/v1/telemetry` | `https://api.nekohoa.com/api/v1/telemetry` | FR-016/FR-030 |
| `propagateTraceHeaderCorsUrls` | n/a (same-origin) | `http://localhost:5212` | `https://api.nekohoa.com` | FR-001 |

---

## Telemetry Proxy (request/response shape)

See `contracts/telemetry-proxy.openapi.yaml`. Summary:
- **Request**: `POST /api/v1/telemetry`, `Content-Type: application/x-protobuf`, body = OTLP
  `ExportTraceServiceRequest` (≤ configured max). Anonymous allowed; rate-limited.
- **Response**: `202 Accepted` (no body) on accept; `413` over size cap; `429` over rate limit.
- **Behavior**: passthrough to server-configured destination; trace/span IDs preserved; scrubbed;
  fire-and-forget; failures never surfaced (FR-008/FR-031).

---

## Out of scope (explicitly no entity)
- No persisted telemetry storage (dashboard retains in memory; vendor stores in prod).
- No `neko-hoa-mock` instrumentation.
- No OTel Collector entity (removed — see Clarifications).
- No browser logs or browser metrics.

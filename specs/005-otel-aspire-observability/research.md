# Phase 0 Research: Full-Stack Observability

**Feature**: 005-otel-aspire-observability | **Date**: 2026-06-05

All architectural unknowns were resolved during the `/speckit.clarify` session (see
`spec.md` → Clarifications). This document records the concrete technical decisions that
implement those clarifications, so Phase 1 design and Phase 2 tasks have no open
`NEEDS CLARIFICATION` items.

---

## R1. Backend OpenTelemetry pipeline ownership

- **Decision**: Use the OpenTelemetry .NET SDK (`OpenTelemetry.Extensions.Hosting`) as the
  single owner of the `Activity`/tracing, metrics, and (via Serilog) logging pipelines.
  Register `AddOpenTelemetry().WithTracing(...).WithMetrics(...)` in `Program.cs` with the
  OTLP exporter set to `http/protobuf`. Instrumentations: `AddAspNetCoreInstrumentation`,
  `AddHttpClientInstrumentation`, `AddNpgsql` (Npgsql's built-in OTel source). Metrics:
  ASP.NET Core + HttpClient meters provide request count, duration histogram, and error rate
  per route (FR-012).
- **Rationale**: Native, supported, vendor-neutral; satisfies FR-023 (single activity owner)
  and FR-015 (direct OTLP export). Npgsql emits SQL spans without an EF interceptor.
- **Alternatives considered**: Manual `ActivitySource` everywhere (rejected: reinvents
  auto-instrumentation); EF Core command interceptor for SQL (rejected: Npgsql's OTel source
  already produces `db.statement` spans and is the documented path).

## R2. OTLP transport — HTTP/protobuf, port 18890

- **Decision**: Set `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` and target the Aspire
  Dashboard's **HTTP** OTLP endpoint (`http://aspire-dashboard:18890`) in Docker Compose;
  signal paths `/v1/traces`, `/v1/logs`, `/v1/metrics`. The .NET exporter defaults to gRPC,
  so the protocol MUST be set explicitly.
- **Rationale**: FR-022 mandates HTTP/protobuf (browser compatibility + uniform config); the
  Aspire Dashboard exposes gRPC on 18889 and HTTP on 18890.
- **Alternatives considered**: gRPC (rejected by FR-022); OTLP/JSON (rejected: protobuf is the
  spec'd encoding and more compact).

## R3. Sentry-on-OTel with independent sampling

- **Decision**: Replace Sentry's standalone tracer. Add `Sentry.OpenTelemetry` and call
  `options.UseOpenTelemetry()` so Sentry consumes OTel activities; wire Sentry's
  `TracesSampler`/`TracesSampleRate` to an env-configurable value (default `0.2`, preserving
  current behavior) so Sentry trace volume is decoupled from the OTel sampler's 100% default.
  Remove the standalone `o.TracesSampleRate = 0.2` independent-tracer assumption; keep
  `SetBeforeSend` and **broaden** its scrub set (emails, names) to match FR-009. Error capture
  remains unconditional.
- **Rationale**: FR-013/FR-023/FR-027 — one activity pipeline, no double-sampling, Sentry quota
  protected, errors always reported.
- **Alternatives considered**: Two parallel tracers (rejected: double spans/sampling — the risk
  the spec exists to remove); Sentry at 100% (rejected by clarification: quota/cost); errors-only
  to Sentry (rejected by clarification in favor of a simple independent rate).

## R4. Serilog → structured JSON + OTLP + trace enrichment

- **Decision**: Keep `UseSerilog`. Add `Serilog.Sinks.OpenTelemetry` (OTLP/HTTP protobuf to the
  same destination) and enrich with trace/span IDs. Keep the human-readable Console sink for dev
  DX; logs sent to the dashboard/vendor use JSON/OTLP. Trace/span IDs come from
  `Activity.Current` via an enricher (the OTLP sink maps TraceId/SpanId natively). Add a
  `TraceEnrichmentMiddleware` (or Serilog enricher) attaching the authenticated user's **subject
  GUID** (`ClaimTypes.NameIdentifier`) — never email (FR-011).
- **Rationale**: FR-003/FR-011/FR-018/FR-019/FR-020 + Constitution §8 (structured JSON, request
  correlation). `NameClaimType = ClaimTypes.NameIdentifier` is already configured, so the GUID is
  the natural identifier.
- **Alternatives considered**: Replace Console with JSON globally (rejected: hurts local console
  readability); write JSON file as the dashboard transport (rejected: OTLP is the unified path).

## R5. SQL text capture, gated

- **Decision**: Capture full SQL on Npgsql spans only when an env flag
  (`Observability__CaptureSqlText`) is true; default **true** in Development, **false** in
  Production. Implement via Npgsql's options / a span processor that strips `db.statement` when
  disabled.
- **Rationale**: FR-004/FR-010 + Constitution Database/runtime gate. SQL text can contain
  parameter values → off by default in prod.
- **Alternatives considered**: Always capture (rejected: leaks data in prod); never capture
  (rejected: US4 needs it in dev).

## R6. PII / financial / credential scrubbing at SDK level

- **Decision**: A single `TelemetryScrubbingProcessor` (OTel `BaseProcessor<Activity>` + a Log
  processor) removes/redacts span attributes and log fields matching a configured key set
  (passwords, tokens, card/account/routing numbers, emails, full names) before export. The same
  field set is applied in Sentry's `SetBeforeSend`. Applies to all destinations (FR-009).
- **Rationale**: FR-009 mandates scrubbing at the instrumentation layer, consistently across
  traces, logs, and Sentry.
- **Alternatives considered**: Scrub at the dashboard/collector (rejected by FR-009 — must be at
  source); regex over serialized payloads (rejected: brittle; attribute-key filtering is precise).

## R7. Telemetry export resilience

- **Decision**: Use the OTLP exporter's batch processor with a **bounded queue** (default size)
  that drops on overflow; no disk persistence; export off the request path. Exporter failures are
  swallowed by the SDK.
- **Rationale**: FR-008 + clarification (bounded in-memory queue, drop on overflow, never block).
- **Alternatives considered**: Persistent file buffer / replay (rejected by clarification:
  complexity, disk mgmt); synchronous export (rejected: blocks requests).

## R8. Frontend OTel Web SDK + instrumentation

- **Decision**: `@opentelemetry/sdk-trace-web` `WebTracerProvider` with `ZoneContextManager`,
  `BatchSpanProcessor` + `OTLPTraceExporter` (HTTP/protobuf) pointed at the proxy URL; register
  `DocumentLoadInstrumentation` + `XMLHttpRequestInstrumentation` (the app's `HttpClient` is
  XHR-based; no `withFetch()`). Configure `propagateTraceHeaderCorsUrls` to the API origin so
  `traceparent` is injected on cross-origin (`ng serve`/prod) requests. Initialize during
  bootstrap via `APP_INITIALIZER` in `app.config.ts`; init must not throw or block startup.
  Emit **traces only** (no browser logs/metrics).
- **Rationale**: FR-001/FR-005/FR-030 + verified codebase facts (Angular 17.3, XHR HttpClient,
  Zone.js, functional interceptors, `environment.*.ts` config).
- **Alternatives considered**: `fetch` instrumentation (rejected: app uses XHR);
  manual `HttpInterceptor` for `traceparent` (rejected by clarification: auto-instrumentation is
  less error-prone for context propagation); `auto-instrumentations-web` meta-package (rejected:
  pulls user-interaction/fetch instrumentations not in scope — traces-only, document-load + XHR).

## R9. Telemetry proxy endpoint (browser egress)

- **Decision**: A FastEndpoint `POST /api/v1/telemetry` (anonymous, rate-limited via a new
  `"telemetry"` fixed-window limiter, configurable body cap default 1 MB). Reads the raw
  OTLP/HTTP protobuf body and forwards it unchanged (passthrough — preserves browser trace/span
  IDs) to the server-configured destination via `HttpClient`, attaching destination headers
  server-side; applies FR-009 scrubbing; returns 202/204 fire-and-forget; never client-specifies
  the destination. Covered by the existing CORS policy (extended to allow the OTLP content type
  and `traceparent`).
- **Rationale**: FR-016/FR-029/FR-031 + Constitution (FastEndpoints only; rate limit public
  endpoints). Replaces the removed collector's browser-CORS + credential-hiding roles with one
  small route; unifies local/prod.
- **Alternatives considered**: Separate OTel Collector container (rejected by clarification:
  extra service, divergent paths); browser → dashboard direct (rejected: no CORS, doesn't hide
  prod vendor key); re-emit spans through backend TracerProvider (rejected: would remap IDs and
  break end-to-end correlation).

## R10. Local dashboard = .NET Aspire Dashboard

- **Decision**: Add `mcr.microsoft.com/dotnet/aspire-dashboard` to `docker-compose.yaml` with
  `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`, OTLP HTTP receiver on 18890, UI on 18888,
  on `app_network`. Local-dev only; not deployed.
- **Rationale**: FR-006 (zero-config, no login token), FR-014; unifies traces+logs+metrics in one
  view.
- **Alternatives considered**: Jaeger (traces only); Grafana/Tempo/Loki/Prometheus (multi-
  container, heavier) — both rejected during clarification.

## R11. Test strategy (no external telemetry service)

- **Decision**: Extend `IntegrationTestBase` to (a) add a real Serilog sink for the `Test`
  environment — a custom in-memory `ILogEventSink` (or `Serilog.Sinks.File` to a temp path,
  `buffered:false`) — and (b) register OTel **in-memory exporters** (`AddInMemoryExporter`) for
  traces and metrics through `ConfigureServices`. Assertions read these. No Aspire Dashboard or
  collector container in the test path. Live OTLP egress is verified manually (quickstart).
- **Rationale**: FR-024/FR-025/SC-008/SC-009 + the existing in-process `WebApplicationFactory` +
  Testcontainers harness. Deterministic, container-free, no flush-timer flakiness.
- **Alternatives considered**: Collector container + file assertions (rejected by clarification:
  collector dropped, flush flakiness); mocking the logger (rejected: SC-008 forbids test doubles).

---

## Resolved unknowns summary

| Topic | Resolution |
|-------|-----------|
| Activity pipeline ownership | OTel single owner; Sentry consumes it (R1, R3) |
| Transport | OTLP HTTP/protobuf, port 18890 (R2) |
| Sentry volume | Independent ~20% sample rate; errors always (R3) |
| Logging | Serilog JSON + OTLP sink + trace/user-id enrichment (R4) |
| SQL capture | Env-gated, off in prod (R5) |
| Scrubbing | Single SDK-level processor + Sentry before-send (R6) |
| Export failure | Bounded in-memory queue, drop on overflow (R7) |
| Frontend SDK | sdk-trace-web + XHR + document-load + zone ctx, traces-only (R8) |
| Browser egress | FastEndpoint passthrough proxy, anon + rate-limited (R9) |
| Local dashboard | .NET Aspire Dashboard, anonymous (R10) |
| Tests | Serilog sink + OTel in-memory exporters, no container (R11) |

**No `NEEDS CLARIFICATION` remain.** Ready for Phase 1.

# Tasks: Full-Stack Observability with Distributed Tracing

**Feature**: 005-otel-aspire-observability
**Input**: Design documents from `/specs/005-otel-aspire-observability/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: REQUIRED — the Constitution mandates test-first (red-green) and the spec defines
Quality Gates + SC-008/SC-009. Test tasks precede implementation within each phase.

**Organization**: Tasks are grouped by user story (priority order) for independent delivery.

## Conventions
- Backend: `HOAManagementCompany/` (.NET 9, FastEndpoints, Serilog, Sentry).
- Tests: `HOAManagementCompany.Tests/` (xUnit + Testcontainers, in-process `WebApplicationFactory`).
- Frontend: `neko-hoa/` (Angular 17.3).
- `[P]` = parallelizable (different files, no incomplete-task dependency).
- No DB schema changes anywhere in this feature.

---

## Phase 1: Setup (dependencies)

- [X] T001 Add backend OpenTelemetry packages to `HOAManagementCompany/HOAManagementCompany.csproj`: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Npgsql.OpenTelemetry`.
- [X] T002 [P] Add Serilog OTLP + JSON packages to `HOAManagementCompany/HOAManagementCompany.csproj`: `Serilog.Sinks.OpenTelemetry`, `Serilog.Formatting.Compact`.
- [X] T003 [P] Add `Sentry.OpenTelemetry` package to `HOAManagementCompany/HOAManagementCompany.csproj` (for Sentry-on-OTel; `Sentry.AspNetCore` already present).
- [X] T004 [P] Add frontend OpenTelemetry packages to `neko-hoa/package.json`: `@opentelemetry/sdk-trace-web`, `@opentelemetry/exporter-trace-otlp-http`, `@opentelemetry/context-zone`, `@opentelemetry/instrumentation-document-load`, `@opentelemetry/instrumentation-xml-http-request`, `@opentelemetry/resources`, `@opentelemetry/semantic-conventions`; run `npm install`.
- [X] T005 [P] Add OTel in-memory exporter package `OpenTelemetry.Exporter.InMemory` to `HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj` (test-only).

---

## Phase 2: Foundational (BLOCKS all user stories)

**Purpose**: Shared observability scaffolding, the single-pipeline correctness, scrubbing, and the
test harness. No user story is verifiable until this phase is complete.

- [X] T006 Create `ObservabilityOptions` (binds `Observability__*` + `OTEL_*` env vars: OtlpEndpoint, OtlpProtocol, OtlpHeaders, ServiceName, TraceSampleRatio, SentryTraceSampleRatio, CaptureSqlText, TelemetryProxyMaxBodyBytes, ScrubbedKeys, LogFilePath/LogRotation) in `HOAManagementCompany/Infrastructure/Observability/ObservabilityOptions.cs` per data-model.md.
- [X] T007 Implement `TelemetryScrubbingProcessor` (OTel `BaseProcessor<Activity>` + a log-record processor) that redacts attributes/fields whose key matches `ScrubbedKeys` (passwords, tokens, card/account/routing numbers, emails, full names) in `HOAManagementCompany/Infrastructure/Observability/TelemetryScrubbingProcessor.cs` (FR-009).
- [X] T008 Create `ObservabilityServiceCollectionExtensions.AddObservability(...)` skeleton in `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs`: registers `AddOpenTelemetry()` with a resource (`service.name` from options), the OTLP exporter set to `http/protobuf` at `OtlpEndpoint`, the head-based sampler from `TraceSampleRatio` (default 1.0), and the scrubbing processor. Tracing/metrics instrumentations are added by their respective stories.
- [X] T009 Reconfigure Sentry to consume OTel (Sentry-on-OTel) in `HOAManagementCompany/Program.cs`: call `options.UseOpenTelemetry()`, set Sentry's own trace sample rate from `SentryTraceSampleRatio` (default 0.2), remove the standalone independent-tracer assumption, and broaden the existing `SetBeforeSend` scrub to the FR-009 field set (add emails, full names). Error capture stays unconditional (FR-013/FR-023).
- [X] T010 Wire `builder.AddObservability()` into `HOAManagementCompany/Program.cs` bootstrap (after Serilog/Sentry, before `builder.Build()`), guarded so telemetry-init failures log a warning but never block startup (US1 AS3 / FR-008).
- [X] T011 Extend test harness in `HOAManagementCompany.Tests/Fixtures/IntegrationTestBase.cs`: in `ConfigureServices`, register OTel `AddInMemoryExporter` for traces and metrics into shared collections, and add a real Serilog JSON sink for the `Test` environment (custom in-memory `ILogEventSink` or `Serilog.Sinks.File` temp path, `buffered:false`); expose accessors for tests to read spans/metrics/log records (FR-024/FR-025).

**Checkpoint**: Backend boots with OTel active; failures are non-fatal; tests can read in-memory
spans/metrics and Serilog records. No signals are story-verified yet.

---

## Phase 3: User Story 1 - Access a Local Observability Dashboard (P1) 🎯 MVP

**Goal**: Backend traces, logs, and metrics are visible together in a zero-config local
.NET Aspire Dashboard; the app runs normally if the dashboard is down.

**Independent test**: Start the stack, open the dashboard (no auth), trigger an API action, and
see its trace, logs, and metrics within 30 s.

### Tests (write first — red)
- [X] T012 [P] [US1] Test: backend HTTP request produces a server span exported to the in-memory trace exporter (asserts a span with `http.request.method`/route) in `HOAManagementCompany.Tests/Integration/Observability/BackendTracingTests.cs`.
- [X] T013 [P] [US1] Test: backend emits request-count + duration histogram + error-rate metrics per endpoint to the in-memory metric reader in `HOAManagementCompany.Tests/Integration/Observability/BackendMetricsTests.cs` (FR-012).
- [X] T014 [P] [US1] Test: an in-flight request writes a structured JSON log record (valid JSON, minimum fields) to the Serilog test sink in `HOAManagementCompany.Tests/Integration/Observability/BackendLoggingTests.cs` (FR-018/FR-019/SC-009).

### Implementation (green)
- [X] T015 [US1] Add ASP.NET Core + HttpClient instrumentation to the tracing pipeline in `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` (`AddAspNetCoreInstrumentation`, `AddHttpClientInstrumentation`).
- [X] T016 [US1] Add metrics pipeline (ASP.NET Core + HttpClient meters → OTLP) in `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` (FR-012).
- [X] T017 [US1] Reconfigure Serilog in `HOAManagementCompany/Program.cs` to add the `Serilog.Sinks.OpenTelemetry` sink (OTLP/HTTP protobuf to `OtlpEndpoint`) emitting JSON; keep the human-readable Console sink for dev (FR-019/FR-020).
- [X] T018 [P] [US1] Add the `aspire-dashboard` service (`mcr.microsoft.com/dotnet/aspire-dashboard`, `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`, OTLP HTTP on 18890, UI on 18888, `app_network`) to `docker-compose.yaml`, and set `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_EXPORTER_OTLP_PROTOCOL`/`OTEL_SERVICE_NAME` on the `api` service (FR-006/FR-014).
- [X] T019 [US1] Add Repowise marker region `domain=observability` around the OTel setup in `ObservabilityServiceCollectionExtensions.cs` and extend the `domain=bootstrap` note in `Program.cs`.

**Checkpoint**: US1 independently testable — backend telemetry visible in the dashboard; app
survives dashboard-down. This is the MVP.

---

## Phase 4: User Story 2 - Trace a Request End-to-End (P1)

**Goal**: A single trace links browser action → API handler → DB query via a shared trace ID,
with browser telemetry reaching the dashboard through the backend telemetry proxy.

**Independent test**: Submit an action from the browser; find one trace containing the browser
span, the API span, and DB spans sharing one trace ID.

### Tests (write first — red)
- [X] T020 [P] [US2] Contract test: `POST /api/v1/telemetry` accepts anonymous OTLP protobuf and returns 202; rejects > body cap with 413 and wrong media type with 415 in `HOAManagementCompany.Tests/Integration/Observability/TelemetryProxyTests.cs` (FR-031, contracts/telemetry-proxy.openapi.yaml).
- [X] T021 [P] [US2] Test: telemetry proxy is rate-limited (429 after the configured window is exhausted) and never forwards to a client-specified destination in `HOAManagementCompany.Tests/Integration/Observability/TelemetryProxyTests.cs` (FR-031).
- [X] T022 [P] [US2] Test: a request carrying an inbound W3C `traceparent` continues the same `trace_id` on the backend server span (in-memory exporter) in `HOAManagementCompany.Tests/Integration/Observability/TracePropagationTests.cs` (FR-002).
- [X] T023 [P] [US2] Frontend test: OTel web SDK initializes without throwing on valid config and injects `traceparent` on an outbound XHR in `neko-hoa/src/app/core/observability/otel.bootstrap.spec.ts` (Jasmine/Karma; Quality Gate).

### Implementation (green)
- [X] T024 [US2] Add a `"telemetry"` fixed-window rate limiter (mirroring the existing `"auth"`/`"payments"` limiters, keyed by client IP) in `HOAManagementCompany/Program.cs` (FR-031).
- [X] T025 [US2] Extend the CORS policy in `HOAManagementCompany/Program.cs` to allow the `traceparent`/`tracestate` request headers and the `application/x-protobuf` content type for the telemetry path (FR-028).
- [X] T026 [US2] Implement the telemetry proxy FastEndpoint `POST /api/v1/telemetry` in `HOAManagementCompany/Features/Observability/TelemetryProxyEndpoint.cs`: anonymous, body-size-capped (`TelemetryProxyMaxBodyBytes`), reads raw OTLP protobuf, applies scrubbing, forwards passthrough (preserving trace/span IDs) to the server-configured destination via `HttpClient` with credentials attached server-side, returns 202 fire-and-forget; add Repowise `domain=observability` marker (FR-016/FR-029/FR-031).
- [X] T027 [P] [US2] Implement the frontend OTel bootstrap in `neko-hoa/src/app/core/observability/otel.bootstrap.ts`: `WebTracerProvider` + `ZoneContextManager` + `BatchSpanProcessor` + `OTLPTraceExporter` (HTTP/protobuf) to `environment.telemetryUrl`; register `DocumentLoadInstrumentation` + `XMLHttpRequestInstrumentation` with `propagateTraceHeaderCorsUrls`; traces only; add Repowise `section=observability` marker (FR-001/FR-030).
- [X] T028 [US2] Wire the OTel bootstrap into app startup via `APP_INITIALIZER` in `neko-hoa/src/app/app.config.ts` (must not throw/block startup) (FR-030).
- [X] T029 [P] [US2] Add `telemetryUrl` + `propagateTraceHeaderCorsUrls` to `neko-hoa/src/environments/environment.ts`, `environment.development.ts`, and `environment.docker.ts` per data-model.md (FR-016).

**Checkpoint**: US2 independently testable — end-to-end browser→API→DB trace under one ID;
browser telemetry flows through the proxy.

---

## Phase 5: User Story 3 - Correlate Logs to a Specific Request (P2)

**Goal**: Every log entry from an in-flight request carries the trace ID, span ID, and the
authenticated user's GUID (never email/PII).

**Independent test**: Make an authenticated request; confirm its logs carry the same trace ID and
the user GUID, and filtering by trace ID isolates only that request's entries.

### Tests (write first — red)
- [X] T030 [P] [US3] Test: all log entries during an in-flight request carry the request's `trace_id` and `span_id` (Serilog test sink) in `HOAManagementCompany.Tests/Integration/Observability/LogEnrichmentTests.cs` (FR-003/SC-003).
- [X] T031 [P] [US3] Test: an authenticated request's log entries carry the user's `NameIdentifier` GUID and NOT the email/username in `HOAManagementCompany.Tests/Integration/Observability/LogEnrichmentTests.cs` (FR-011).

### Implementation (green)
- [X] T032 [US3] Add trace/span enrichment to Serilog (so every log record carries `trace_id`/`span_id` from `Activity.Current`) in `HOAManagementCompany/Program.cs` Serilog configuration (FR-003).
- [X] T033 [US3] Implement `TraceEnrichmentMiddleware` attaching the authenticated user's subject GUID (`ClaimTypes.NameIdentifier`) to the log context for the request scope in `HOAManagementCompany/Infrastructure/Observability/TraceEnrichmentMiddleware.cs`, and register it in `HOAManagementCompany/Program.cs` (FR-011).

**Checkpoint**: US3 independently testable — logs correlate to a trace and carry the user GUID.

---

## Phase 6: User Story 4 - View Database Query Performance in Traces (P2)

**Goal**: DB spans show the exact SQL text (and per-operation duration) in dev; SQL text capture
is env-gated and OFF by default in production.

**Independent test**: Call a DB-reading endpoint; confirm a span carries `db.statement` SQL text
in dev, and that the capture flag toggles it without code changes.

### Tests (write first — red)
- [X] T034 [P] [US4] Test: a DB-reading request produces span(s) with `db.statement` SQL text and individual durations when capture is enabled (in-memory exporter) in `HOAManagementCompany.Tests/Integration/Observability/SqlSpanCaptureTests.cs` (FR-004).
- [X] T035 [P] [US4] Theory test: `CaptureSqlText` true → SQL text present; false → SQL text absent/stripped, in `HOAManagementCompany.Tests/Integration/Observability/SqlSpanCaptureTests.cs` (FR-010, xUnit `[Theory]`).

### Implementation (green)
- [X] T036 [US4] Add Npgsql instrumentation to the tracing pipeline (`AddNpgsql()`) in `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` (FR-004).
- [X] T037 [US4] Gate SQL text on `CaptureSqlText` (default true in Dev, false in Prod): configure Npgsql to include/exclude the SQL statement, or strip `db.statement` in the scrubbing processor when disabled, in `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` / `TelemetryScrubbingProcessor.cs` (FR-010).

**Checkpoint**: US4 independently testable — SQL visible in dev traces, gated off in prod.

---

## Phase 7: User Story 5 - Switch Telemetry Target for Production (P3)

**Goal**: An operator repoints telemetry to a cloud vendor by changing ≤3 env vars — no code,
rebuild, or migration — and telemetry appears in the vendor's dashboard.

**Independent test**: Change `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_EXPORTER_OTLP_HEADERS`/
`OTEL_SERVICE_NAME`, restart, and confirm telemetry routes to the new destination with no source
changes; unset → defaults to local.

### Tests (write first — red)
- [X] T038 [P] [US5] Test: `ObservabilityOptions` binds endpoint/headers/service-name from environment and the exporter targets the configured endpoint; unset endpoint in Development defaults to the local dashboard, in `HOAManagementCompany.Tests/Integration/Observability/TelemetryConfigTests.cs` (FR-007/SC-004, US5 AS3).
- [X] T039 [P] [US5] Test: the telemetry proxy attaches the server-configured destination headers (vendor credential) and the destination is never taken from the request, in `HOAManagementCompany.Tests/Integration/Observability/TelemetryProxyTests.cs` (FR-029).

### Implementation (green)
- [X] T040 [US5] Confirm/finish env-var-only destination switching in `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` (backend exporter) and `TelemetryProxyEndpoint.cs` (proxy forward target read from options/env) — no hard-coded endpoints (FR-007/FR-029).
- [X] T041 [P] [US5] Document the production switch (≤3 env vars, frontend proxy keeps credentials server-side, defaults) in `HOAManagementCompany/appsettings.json` comments and `specs/005-otel-aspire-observability/quickstart.md` (already drafted — verify accuracy) (SC-004).

**Checkpoint**: US5 independently testable — vendor switch via env vars only.

---

## Phase 8: Polish & Cross-Cutting

- [X] T042 [P] Validate the quickstart end-to-end (run `docker compose up`, see a browser→API→DB trace in the dashboard within 30 s) per `specs/005-otel-aspire-observability/quickstart.md` (SC-002/SC-007).
- [X] T043 [P] Measure telemetry overhead against `HOAManagementCompany.Tests/Performance/DashboardPerformanceTests.cs` baseline and confirm ≤2% p95 increase (SC-006).
- [X] T044 [P] Confirm scrubbing coverage: no email/name/password/token/card/account/routing values appear in exported spans or log records (extend `ScrubbingTests.cs`) (FR-009).
- [X] T045 [P] Verify ≥95% coverage on changed/added files and that Sonar/Codecov gates pass; ensure all backend integration + frontend unit tests are green (Constitution completion gate).
- [X] T046 [P] Run the Repowise workflow and commit refreshed marker regions for the touched files (Constitution CI/CD §9).

---

## Dependencies & Execution Order

```
Phase 1 (Setup: T001–T005)
        ↓
Phase 2 (Foundational: T006–T011)   ← BLOCKS all stories
        ↓
   ┌────┴───────────────┬───────────────┬───────────────┐
Phase 3 US1 (MVP)   Phase 5 US3      Phase 6 US4      Phase 7 US5
 T012–T019           T030–T033        T034–T037        T038–T041
        ↓
Phase 4 US2 (T020–T029)   ← depends on US1 (dashboard + backend export) for full E2E
        ↓
Phase 8 (Polish: T042–T046)
```

**Story dependency notes**:
- **US1** depends only on Foundational. It is the MVP.
- **US2** builds on US1 (the dashboard + backend export must exist for the end-to-end trace to be
  visible) and adds the frontend + proxy. The proxy/propagation **tests** (T020–T023) can be
  written against Foundational, but the full E2E acceptance needs US1.
- **US3, US4, US5** depend only on Foundational and are **independent of each other** — they can be
  implemented in parallel after Phase 2 (and after US1 for dashboard verification).
- Within every story, tests (red) precede implementation (green).

## Parallel Execution Examples

- **Setup**: T002, T003, T004, T005 run in parallel after T001.
- **US1 tests**: T012, T013, T014 in parallel (different files), then implementation.
- **US1 impl**: T018 (compose) ∥ T015–T017 (backend code) — different files.
- **Cross-story after Phase 2**: a developer can take US3 (T030–T033) while another takes
  US4 (T034–T037) and another US5 (T038–T041) — disjoint files.
- **US2**: T027/T029 (frontend) ∥ T024/T025/T026 (backend) — different projects.
- **Polish**: T042–T046 largely parallel.

## Implementation Strategy

- **MVP = Phase 1 + Phase 2 + Phase 3 (US1)**: backend telemetry visible in the local dashboard,
  app resilient to dashboard-down. Demoable and independently valuable.
- **Increment 2 = US2**: end-to-end browser→API→DB tracing via the proxy (the headline capability).
- **Increment 3 = US3 + US4** (parallelizable): log correlation and SQL-in-traces.
- **Increment 4 = US5**: production vendor portability.
- **Finish with Phase 8**: performance, scrubbing, coverage, Repowise, quickstart validation.

## Notes
- No database migrations in any task (telemetry is runtime/middleware only).
- Tests use the existing in-process `WebApplicationFactory` + Testcontainers harness; **no Aspire
  Dashboard or collector container** is started by the test suite (FR-024/FR-025).
- Keep telemetry-init non-fatal everywhere (FR-008): a misconfigured/unavailable destination must
  never block startup or fail a request.

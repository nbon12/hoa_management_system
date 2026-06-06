# Implementation Plan: Full-Stack Observability with Distributed Tracing

**Branch**: `005-otel-aspire-observability` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/005-otel-aspire-observability/spec.md`

## Summary

Add full-stack OpenTelemetry observability across the `neko-hoa` Angular frontend and the
`HOAManagementCompany` .NET backend, viewable in a zero-config local **.NET Aspire Dashboard**.
The backend exports traces, logs, and metrics via **OTLP/HTTP (protobuf)** directly to the
dashboard (local) or a cloud vendor (production). The frontend emits **traces only** via the
OpenTelemetry JS Web SDK and exports them through a new **same-API telemetry proxy endpoint**
(`POST /api/v1/telemetry`) so vendor credentials never reach the browser and the local/prod
paths are identical. **OpenTelemetry owns the single activity pipeline**; Sentry is reconfigured
to consume it (Sentry-on-OTel) with its own independent trace sample rate. Serilog is extended
to emit structured JSON enriched with trace/span IDs. No database schema changes; no changes to
existing API contracts beyond the one additive telemetry endpoint. Integration tests assert on a
real Serilog sink + OTel in-memory exporters — no external telemetry container in the test path.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend)
**Primary Dependencies**:
- Backend: OpenTelemetry .NET SDK (`OpenTelemetry.Extensions.Hosting`,
  `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`,
  `Npgsql.OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` with HTTP/protobuf),
  `Serilog.Sinks.OpenTelemetry`, `Serilog.Formatting.Compact`, existing `Sentry.AspNetCore`
  (reconfigured via `Sentry.OpenTelemetry`).
- Frontend: `@opentelemetry/sdk-trace-web`, `@opentelemetry/exporter-trace-otlp-http`,
  `@opentelemetry/context-zone`, `@opentelemetry/instrumentation-document-load`,
  `@opentelemetry/instrumentation-xml-http-request`, `@opentelemetry/resources`,
  `@opentelemetry/semantic-conventions`.
- Infra: `.NET Aspire Dashboard` standalone container (`mcr.microsoft.com/dotnet/aspire-dashboard`).

**Storage**: PostgreSQL (existing) — **no schema changes, no migrations** in this feature.
**Testing**: xUnit + .NET Testcontainers via in-process `WebApplicationFactory<Program>`
(existing harness); real Serilog JSON sink (in-memory/file) for log assertions; OTel
**in-memory exporters** for trace/metric assertions; Jasmine + Karma for the Angular OTel-init
unit test. No external telemetry service in the test path.
**Target Platform**: Linux container on Google Cloud Run (backend); modern browsers (Angular SPA
via Cloudflare Pages); Docker Compose for local development.
**Project Type**: Web application (separate frontend + backend + integration test suite).
**Performance Goals**: ≤2% p95 API latency overhead vs. telemetry-disabled baseline (SC-006);
browser-to-dashboard end-to-end trace visible ≤30 s (SC-002); 100% of in-flight log entries
carry the trace ID (SC-003).
**Constraints**: OTLP over HTTP/protobuf only (no gRPC, FR-022); bounded in-memory export queue
that drops on overflow, never blocks the request path (FR-008); telemetry failures never surface
to users; no schema migrations; no existing API-contract changes (one additive endpoint only);
PII/financial/credential scrubbing at the SDK/exporter level (FR-009); SQL text capture gated by
env var, off in production (FR-010).
**Scale/Scope**: Infrastructure/middleware-level change. ~5 backend wiring areas (OTel bootstrap,
Serilog reconfig, Sentry-on-OTel, EF/Npgsql instrumentation, telemetry proxy endpoint), 1
frontend SDK bootstrap, 1 Docker Compose service, plus tests. No HOA domain feature changes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Confirm the plan satisfies the active HOA Management Company Constitution v2.0.0:

- **Technology fit**: ✅ Angular frontend and .NET FastEndpoints backend retained. The telemetry
  proxy is implemented as a **FastEndpoint** (no MVC controller). **Serilog** remains the logging
  library (§8/§9) and is extended, not replaced. **Sentry** remains the error/trace tool (§2/§8) —
  reconfigured as a consumer of the OTel pipeline (Sentry-on-OTel), still satisfying "Sentry MUST
  be used for error tracking … and trace context across frontend and backend." **Swashbuckle**
  remains dev-only; the new endpoint appears in dev Swagger. .NET Aspire Dashboard is a
  **local-dev-only** container (not deployed). *Not applicable this feature*: Neon, Cloud Run,
  Cloudflare, Docker Hub config (deployment-time), Auth0 (the repo currently uses JWT bearer; auth
  migration is out of scope and unchanged by this feature).
- **HOA tenancy**: ✅ No HOA-scoped data is created or queried. Telemetry MUST NOT carry HOA/
  association/community identifiers as trace attributes in shared dashboards (spec Tenant-boundary
  constraint). No cross-HOA query surface introduced.
- **API contracts**: ✅ One **additive** endpoint (`POST /api/v1/telemetry`), documented in
  `contracts/`. Returns 202/204, no body; not a collection (pagination N/A); auth = anonymous
  permitted by design (pre-login trace capture), rate-limited; not cacheable. No existing contract
  changes; trace IDs never appear in response bodies.
- **Security and operations**: ✅ Secrets externalized via env vars (OTLP endpoint, vendor headers,
  Sentry DSN). Server-side scrubbing of PII/financial/credentials at the SDK level (FR-009) and the
  Sentry before-send scrub broadened to match. Production errors unaffected; telemetry failures
  swallowed. Structured **Serilog JSON** logs with correlation (trace/span IDs) — satisfies §8
  "structured JSON logs" + "request correlation IDs." The anonymous telemetry endpoint is
  **rate-limited** (§7 requires rate limiting on public endpoints).
- **File storage**: ✅ N/A — no file/blob storage introduced. (MinIO/R2 untouched.)
- **Caching/edge**: ✅ The telemetry endpoint is POST and MUST NOT be edge-cached. No
  authenticated responses cached. Frontend static assets keep hashed filenames (unchanged).
- **Testing discipline**: ✅ Test-first (red-green) for the telemetry endpoint and SDK init.
  Backend integration tests use **PostgreSQL via Testcontainers** + transaction isolation (existing
  harness). Data-varied cases (e.g., scrubbing field matrix, sampling on/off) use **xUnit
  Theories**. Frontend OTel-init test uses **Jasmine/Karma**. ≥95% coverage on changed files.
- **CI/CD and documentation**: ✅ Sonar/Codecov/coverage gates apply to changed files. Repowise
  marker regions added to touched bootstrap files and updated in the PR. Deployment env isolation
  unaffected (telemetry destination is env-var-driven per environment).

**Gate result: PASS** — no violations requiring Complexity Tracking. (Auth0-vs-JWT is a
pre-existing repo state, explicitly out of scope; not introduced or worsened by this feature.)

## Project Structure

### Documentation (this feature)

```text
specs/005-otel-aspire-observability/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (telemetry signal entities — conceptual, no DB)
├── quickstart.md        # Phase 1 output (run the stack + see traces)
├── contracts/           # Phase 1 output (telemetry proxy endpoint contract)
│   ├── telemetry-proxy.openapi.yaml
│   └── README.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already present)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/                      # .NET 9 backend (FastEndpoints)
├── Program.cs                             # MODIFY: OTel bootstrap, Serilog→OTLP+JSON,
│                                          #         Sentry-on-OTel, EF/Npgsql instrumentation,
│                                          #         CORS (traceparent), telemetry rate limiter
├── Features/
│   └── Observability/                     # NEW: telemetry proxy FastEndpoint
│       └── TelemetryProxyEndpoint.cs
├── Infrastructure/
│   └── Observability/                     # NEW: OTel setup, scrubbing processor, enrichers,
│       │                                  #      options binding (endpoint, sampling, SQL gate)
│       ├── ObservabilityOptions.cs
│       ├── TelemetryScrubbingProcessor.cs
│       ├── TraceEnrichmentMiddleware.cs   # user-id (sub GUID) + trace/span enrichment
│       └── ObservabilityServiceCollectionExtensions.cs
└── appsettings*.json                      # MODIFY: Serilog JSON formatter, OTel defaults

HOAManagementCompany.Tests/                # xUnit + Testcontainers (in-process WAF)
├── Fixtures/
│   └── IntegrationTestBase.cs             # MODIFY: register Serilog test sink + OTel in-memory exporters
└── Integration/Observability/            # NEW
    ├── LogEnrichmentTests.cs              # trace/span/user-id on log entries (FR-003/011)
    ├── SqlSpanCaptureTests.cs             # SQL text in span, gated (FR-004/010)
    ├── TelemetryProxyTests.cs             # endpoint: passthrough, anon, size cap, rate limit (FR-031)
    └── ScrubbingTests.cs                  # PII/financial/credential scrubbing (FR-009) — Theory

neko-hoa/                                  # Angular 17.3 frontend
├── src/app/app.config.ts                  # MODIFY: provide OTel init (APP_INITIALIZER)
├── src/app/core/observability/            # NEW: OTel web SDK bootstrap
│   ├── otel.bootstrap.ts                  # sdk-trace-web + zone ctx + instrumentations + exporter
│   └── otel.bootstrap.spec.ts             # Jasmine/Karma init test (Quality Gate)
└── src/environments/environment*.ts       # MODIFY: telemetryUrl + propagateTraceHeaderCorsUrls

docker-compose.yaml                        # MODIFY: add `aspire-dashboard` service
```

**Structure Decision**: Web application (Constitution §2). The feature is a cross-cutting
infrastructure slice touching backend bootstrap, a single new backend endpoint, frontend bootstrap,
the test harness, and Docker Compose. No new project; no domain-feature directories. New code is
grouped under `Infrastructure/Observability` and `Features/Observability` (backend) and
`core/observability` (frontend) to keep the concern isolated and Repowise-documentable.

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Program.cs` | `domain=bootstrap` | Existing region; extend to note OTel/Serilog/Sentry wiring |
| `HOAManagementCompany/Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` | `domain=observability` | OTel/exporter/sampler/scrubbing setup |
| `HOAManagementCompany/Features/Observability/TelemetryProxyEndpoint.cs` | `domain=observability` | Browser telemetry proxy contract + abuse controls |
| `neko-hoa/src/app/core/observability/otel.bootstrap.ts` | `section=observability` | Frontend OTel SDK bootstrap + propagation config |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=observability -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> No constitution violations to justify. Section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

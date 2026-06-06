# Specification Quality Checklist: Full-Stack Observability with Distributed Tracing

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-06-05
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

> **Note on technical language**: This is a developer-tooling feature whose primary stakeholders are developers and operators. Terms like "trace", "span", "SQL query text", and "log entry" are the domain vocabulary of the feature itself — analogous to using "invoice" in a billing spec. These are acceptable and intentional.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- All items pass. Spec is ready for `/speckit.clarify` (optional) or `/speckit.plan`.
- `FR-020` explicitly names Serilog as the logging library — an intentional stakeholder-directed constraint, not an implementation assumption. This is the only named-framework exception to the technology-agnostic principle and is documented here.
- **No OTel Collector.** The clarification session removed the collector intermediary: its only concrete justification (a log file for tests) was eliminated when tests moved to a Serilog sink, and its CORS role is served by the same-origin backend proxy. Topology is now: .NET backend → Aspire Dashboard (direct OTLP); browser → same-origin backend telemetry proxy → Aspire Dashboard/vendor. The collector is re-addable later via env var (tail-sampling/multi-vendor fan-out) with no app code change. FR-017 and FR-026 are marked Removed; FR-014/015/016 repurposed accordingly.
- The local dashboard is pinned to the standalone **.NET Aspire Dashboard** in anonymous local mode (FR-006).
- **Frontend (Angular 17.3) scope pinned**: emits **traces only** (document-load + XHR/HttpClient spans + JS errors) via the OpenTelemetry JS Web SDK with `instrumentation-xml-http-request` + `instrumentation-document-load` + `ZoneContextManager` (FR-030). FR-005 reconciled (frontend = traces; backend = traces+logs+metrics). Cross-origin `traceparent` propagation requires `propagateTraceHeaderCorsUrls` in production (FR-001).
- Integration-test verification depends on no external telemetry service: log assertions read a real Serilog JSON sink and trace/metric assertions use OTel in-memory exporters, matching the existing in-process `WebApplicationFactory` + Testcontainers harness.
- Sentry runs as a consumer of the unified OpenTelemetry activity pipeline (Sentry-on-OTel), not as a parallel tracer (FR-023). It keeps its OWN env-configurable trace sample rate (default ~20%, today's behavior) so Sentry quota is decoupled from OTel's 100% default; error capture is unaffected by trace sampling. Sentry PII scrubbing is broadened to match the FR-009 field set.
- The logged user identifier is pinned to the subject/`NameIdentifier` GUID, never the email (FR-011) — important because email is the username in this system.
- Trace sampling defaults to 100% in dev and prod, env-var-tunable (FR-027). Frontend telemetry routes through a backend proxy endpoint on the existing API in BOTH environments (FR-016/FR-029), so vendor credentials never reach the browser.
- **Telemetry proxy endpoint fully specified (FR-031)**: additive `POST /api/v1/telemetry` on the existing API (not a separate container); OTLP/HTTP protobuf passthrough preserving browser trace/span IDs; server-configured destination only (no open relay); anonymous-permitted; rate-limited + 1 MB body cap; FR-009 scrubbing; 202/204 fire-and-forget. Backend's own telemetry exports directly with no endpoint. Same-origin in the nginx Docker stack; cross-origin (within existing CORS policy) for `ng serve` and the prod `api.` subdomain.
- Production vendor for telemetry is not selected in this spec — deferred to deployment planning.
- **Clarifications**: see `spec.md` → Clarifications → Session 2026-06-05 (dashboard product, prod sampling rate, collector-down behavior, collector removal).
- **Last updated**: 2026-06-05 (clarify session: Aspire Dashboard pinned, 100% sampling default, bounded-drop on export failure, OTel Collector removed; repurposed FR-014/015/016, removed FR-017/026, revised FR-018/019/020/021/022/028/029, SC-006/009).

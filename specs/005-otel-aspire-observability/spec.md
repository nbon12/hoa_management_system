# Feature Specification: Full-Stack Observability with Distributed Tracing

**Feature Branch**: `005-otel-aspire-observability`  
**Created**: 2026-06-05  
**Status**: Implemented  
**Input**: User description: "Add OpenTelemetry and .NET Aspire Observability"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Access a Local Observability Dashboard (Priority: P1)

A developer running the application locally has access to a single web-based dashboard that shows live traces, logs, and metrics from both the Angular frontend and the .NET backend — without configuring any external services, API keys, or cloud accounts.

**Why this priority**: Zero-config local observability removes the barrier to adoption. If developers need external accounts or manual setup to see traces, adoption collapses and the entire investment is wasted.

**Independent Test**: Start the application stack locally and navigate to the local dashboard URL. Traces from a browser action must appear within 30 seconds of the action completing — with no prior setup or account creation.

**Acceptance Scenarios**:

1. **Given** the application is started locally, **When** a developer navigates to the local dashboard URL, **Then** the dashboard loads with no additional authentication or configuration required.
2. **Given** a browser action in the Angular app (e.g., loading the resident dashboard), **When** the developer views the local observability dashboard, **Then** a trace originating from that browser action appears within 30 seconds.
3. **Given** the observability dashboard is not running, **When** the application starts, **Then** the application starts and operates normally; telemetry failures produce a warning but do not block startup.
4. **Given** the local development stack is running, **When** the .NET backend emits logs, **Then** those logs are exported via OTLP directly to the local Aspire Dashboard and are visible there, correlated with their traces.

---

### User Story 2 - Trace a Request End-to-End (Priority: P1)

A developer or support engineer investigating a user-reported issue (e.g., "my payment failed") opens the local dashboard, finds the affected request, and follows a single trace from the browser click through the API handler to the exact database queries that executed — all in one view.

**Why this priority**: End-to-end request tracing is the foundational debugging capability. Without it, every other observability signal is disconnected. This directly reduces mean time to resolution for production incidents and eliminates multi-tool context switching during local debugging.

**Independent Test**: Submit a payment from the browser, then find the resulting trace in the dashboard. Confirm the trace shows the browser span, the API handler span, and the associated database query span — all linked by a shared trace identifier.

**Acceptance Scenarios**:

1. **Given** the app is running locally, **When** a user submits a payment from the browser, **Then** a single trace appears in the dashboard containing spans for the browser action, the API handler, and the associated database queries — linked by a shared trace identifier.
2. **Given** a request that fails with an error, **When** a developer views the trace, **Then** the error and its originating location (which API call or database query failed) are visible within the trace.
3. **Given** a long-running request, **When** a developer views the trace, **Then** the time spent in each phase (frontend, API processing, database) is visible as distinct spans with individual durations.

---

### User Story 3 - Correlate Logs to a Specific Request (Priority: P2)

A developer investigating a complaint can take a trace ID from the dashboard and find all backend log entries that belong exactly to that request — tagged with the same trace identifier, user ID, and environment. No manual log scraping or timestamp guessing required.

**Why this priority**: Log correlation is the second-most-used debugging tool after tracing. It provides narrative context ("validation failed because field X was null") alongside the structural trace view, which together answer the full "what happened and why" question.

**Independent Test**: Make an API request, capture its trace ID from the dashboard, filter the log output by that trace ID, and confirm only log entries from that specific request are returned — in chronological order and tagged with the user ID.

**Acceptance Scenarios**:

1. **Given** any inbound API request, **When** the request completes, **Then** all log entries generated during that request carry the same trace identifier and span identifier.
2. **Given** an authenticated request, **When** logs are reviewed for that trace, **Then** the user ID (not personal data) is present on every log entry for that request.
3. **Given** a trace ID from the dashboard, **When** logs are filtered by that ID, **Then** only entries belonging to that specific request are returned, with no entries from other concurrent requests mixed in.
4. **Given** the integration test suite runs against the in-process test host, **When** a test triggers an API request that produces log output, **Then** the test can read the entries from the real Serilog JSON sink configured for the Test environment and assert on them — with no mocking or test double required for log verification.

---

### User Story 4 - View Database Query Performance in Traces (Priority: P2)

A developer profiling a slow API endpoint views the exact SQL statements that executed during the request — including their duration — directly within the trace. No separate database profiling tool or query logging configuration is required.

**Why this priority**: Database queries are a common performance bottleneck in HOA operations (payment processing, violation lookups, community queries). Seeing SQL text in traces eliminates guesswork about which query is slow and makes optimization immediately actionable.

**Independent Test**: Call an API endpoint that reads from the database. Confirm the resulting trace contains a span with the exact SQL query text and its execution duration.

**Acceptance Scenarios**:

1. **Given** any API request that performs a database operation, **When** the trace is viewed in the dashboard, **Then** at least one span shows the exact SQL query text that executed.
2. **Given** multiple database operations in a single request, **When** the trace is viewed, **Then** each operation appears as a separate span with its individual duration.
3. **Given** the application running in production, **When** SQL capture behavior is evaluated, **Then** the capture of SQL query text is controlled by an environment variable that defaults to off in production.

---

### User Story 5 - Switch Telemetry Target for Production (Priority: P3)

When preparing a production deployment, an operator updates a small set of environment variables to point the application's telemetry to a cloud observability vendor (Grafana Cloud, Honeycomb, Azure Monitor, or similar). No code changes, re-builds, or schema migrations are required. The same traces, logs, and metrics appear in the new vendor's dashboard immediately after restart.

**Why this priority**: Production portability protects the organization from vendor lock-in. Without environment-variable-driven switching, changing vendors requires a code change and a full release cycle — significantly raising the cost of migration.

**Independent Test**: Update environment variables only (endpoint URL, authentication header, service name), restart the application, and confirm telemetry appears in the target vendor's dashboard without any source file modifications.

**Acceptance Scenarios**:

1. **Given** the application is deployed with production environment variables, **When** the telemetry endpoint and credentials are updated via environment variables, **Then** traces and logs appear in the new vendor's dashboard within one application restart.
2. **Given** only environment variable changes, **When** the application restarts, **Then** no source code modifications, re-builds, or database migrations are required.
3. **Given** environment variables for the telemetry endpoint are unset, **When** the application starts in development mode, **Then** telemetry defaults to the local dashboard.

---

### Edge Cases

- What happens when the telemetry export destination is unreachable? The application must continue operating normally; telemetry failures must not affect user-facing API responses or response codes.
- What happens when the telemetry destination is unavailable (e.g., the Aspire Dashboard container has not started yet)? The .NET backend must start and serve requests normally. Telemetry is held in a bounded in-memory export queue; on overflow the oldest records are dropped. There is no disk buffering and the export path must never block or fail the request.
- What happens when a browser request fails before reaching the backend (network error)? The frontend trace is still captured and visible in the dashboard, marked as incomplete or errored.
- What sensitive data might appear in traces or logs? PII (names, emails), financial data (payment amounts, account details), and authentication credentials (tokens, passwords) must not appear in trace attributes or log fields.
- What happens when telemetry volume spikes? Telemetry must be sampled or dropped without impacting application throughput or response times.
- What happens if the local dashboard is started after the application is already running? Previously buffered telemetry may not appear, but new telemetry from that point forward appears within 30 seconds.
- What happens to telemetry storage in long-running local stacks? The Aspire Dashboard retains telemetry in memory with a bounded, self-evicting buffer, so there is no unbounded disk growth in local development. Any Serilog file sink used by the test suite writes to a temp path and is cleaned up by the test lifecycle; if a persistent dev file sink is enabled, its path and rotation policy MUST be configurable (see FR-021).

## Clarifications

### Session 2026-06-05

- Q: Which product provides the local observability dashboard? → A: .NET Aspire Dashboard (standalone container, anonymous local mode)
- Q: What is the default production trace sampling rate? → A: 100% (sample all traces by default; rate remains env-var configurable to dial down under load/cost pressure)
- Q: How does the backend behave when the telemetry collector is unreachable? → A: Bounded in-memory export queue that drops on overflow — no disk buffering, never blocks the request path
- Q: Now that tests no longer read a collector file, is the OTel Collector intermediary still needed? → A: No — drop it. Backend exports OTLP directly to the .NET Aspire Dashboard (local) / vendor (prod); browser telemetry routes through a same-origin backend proxy endpoint (unifying local and production). The collector is re-introducible later via env var if tail-sampling or multi-vendor fan-out is ever required.
- Q: What telemetry should the Angular frontend emit? → A: Traces only (document-load, XHR/HttpClient requests, JS errors). Browser logs and metrics are out of scope; FR-005 is reconciled so the frontend contributes traces while the backend contributes traces + logs + metrics.
- Q: How should the frontend create spans and inject trace context? → A: OpenTelemetry JS Web SDK with `instrumentation-document-load` + `instrumentation-xml-http-request` auto-instrumentation and `ZoneContextManager` (the app uses XHR-based HttpClient and Zone.js). `traceparent` is injected automatically; cross-origin propagation to the API is enabled via `propagateTraceHeaderCorsUrls`.
- Q: How does browser telemetry physically reach the dashboard/vendor, and what is the proxy endpoint's design? → A: Through an additive endpoint on the existing API (`POST /api/v1/telemetry`, FR-031) — not a separate Docker service. It is a passthrough that preserves the browser's trace/span IDs, forwards to a server-configured destination (dashboard local / vendor prod) with credentials attached server-side, permits anonymous requests, is rate-limited + body-size-capped, applies FR-009 scrubbing, and returns 202/204 fire-and-forget. The backend's own telemetry exports directly (no endpoint); only browser telemetry uses the proxy. Same code path in both environments — only the destination env var differs.
- Q: Under Sentry-on-OTel with OTel at 100%, how is Sentry's trace volume handled? → A: Sentry samples independently — it keeps its own env-configurable trace sample rate (default ~20%, preserving current behavior) applied on top of the OTel activities it consumes, so Sentry quota/cost is decoupled from OTel's 100% default. Error/exception capture is unaffected by trace sampling (all errors reported). See FR-023/FR-027.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Angular frontend MUST generate a trace context for each outbound HTTP request and include the W3C `traceparent` header so the backend can continue the same trace. For cross-origin API calls (e.g., production `nekohoa.com` → `api.nekohoa.com`), the frontend MUST be configured to propagate the trace header to the API origin (`propagateTraceHeaderCorsUrls`); same-origin local-dev calls require no additional configuration.
- **FR-002**: The backend MUST receive and continue the trace context propagated from the browser for every inbound request, linking frontend and backend spans under a single trace identifier.
- **FR-003**: The backend MUST attach the active trace identifier to every structured log entry emitted during an in-flight request.
- **FR-004**: When SQL capture is enabled (per FR-010), the backend MUST capture the full SQL query text for every database operation and attach it as an attribute of the corresponding trace span.
- **FR-005**: The local development dashboard MUST show, in a single correlated view: traces from BOTH the Angular frontend and the .NET backend, and logs and metrics from the .NET backend. (The frontend contributes traces only — see FR-030; browser logs and metrics are out of scope for this feature.)
- **FR-006**: The local development dashboard MUST be the standalone .NET Aspire Dashboard container, running in anonymous/unsecured local mode (e.g., `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`), and MUST be accessible without any external account creation, API key setup, login token, or manual configuration steps.
- **FR-007**: The telemetry export destination — including endpoint URL, authentication headers, and service name — MUST be fully configurable via environment variables with no application source code changes required.
- **FR-008**: Telemetry failures (exporter unreachable, quota exceeded, timeout) MUST NOT propagate as errors to the application and MUST NOT affect user-facing API responses. Telemetry MUST be exported via a bounded in-memory queue that drops records on overflow rather than blocking, retrying synchronously, or persisting to disk.
- **FR-009**: Sensitive data — including user PII (email addresses, full names), financial identifiers, and authentication credentials — MUST be excluded from trace attributes and log fields at the instrumentation layer, not at the dashboard. The existing Sentry before-send scrub (currently limited to card/account/routing fields) MUST be broadened to the same field set so that all three sinks — traces, logs, and Sentry events — enforce a single consistent scrubbing policy.
- **FR-010**: SQL query text capture in traces MUST be disabled by default in production and controllable via an environment variable.
- **FR-011**: The backend MUST enrich log entries with the authenticated user's identifier for all requests from authenticated users. This identifier MUST be the user's subject/`NameIdentifier` claim (the user GUID) and MUST NOT be the user's email address or any other personal data — note that this system uses email as the username, so the email MUST NOT be used as the logged identifier.
- **FR-012**: The system MUST emit metrics covering at minimum: request count, request duration distribution, and error rate per API endpoint.
- **FR-013**: The existing Serilog structured logging pipeline MUST remain operational and unaffected by the addition of the new telemetry pipeline. Sentry error-capture capability (exception reporting, release tracking, environment tagging) MUST remain fully functional; however, Sentry's internal tracer MUST be replaced by the unified OTel pipeline per FR-023.
- **FR-014**: The local development Docker Compose stack MUST include the standalone .NET Aspire Dashboard as a service. No separate telemetry-collector intermediary service is used; the dashboard is the OTLP receiver for local development.
- **FR-015**: The .NET backend MUST export all telemetry — traces, metrics, and logs — via OTLP directly to the configured telemetry destination: the Aspire Dashboard in local development, or a cloud vendor endpoint in production. No intermediary collector is required in the egress path.
- **FR-016**: Browser-originated telemetry MUST be exported to a telemetry proxy endpoint on the application's own backend API (detailed in FR-031), which forwards it to the configured telemetry destination (Aspire Dashboard in local development, vendor in production). This unifies the frontend telemetry path across environments and ensures vendor credentials are never exposed in browser-accessible code. The proxy MUST NOT alter or drop the telemetry payload beyond attaching destination credentials and applying the scrubbing policy of FR-009.
- **FR-017**: *(Removed — superseded by FR-024. With the collector dropped, there is no collector file sink; integration-test log assertions read the real Serilog JSON sink.)*
- **FR-018**: All structured log output — emitted to the Aspire Dashboard (local dev) and to the Serilog test sink (integration tests) — MUST include at minimum: timestamp, severity level, message, trace identifier, span identifier, and all enriched fields, in valid JSON format.
- **FR-019**: The .NET backend MUST emit logs in structured JSON format (Serilog CLEF via `CompactJsonFormatter`) to stdout so that Google Cloud Run's log ingestion can parse, group, and correlate entries without custom transformation. The human-readable text template MUST NOT be used in any environment.
- **FR-020**: Serilog MUST be used as the structured logging library in the .NET backend and MUST be configured to output logs in structured JSON format both to stdout (FR-019) and to the configured OTLP destination endpoint.
- **FR-021**: Where a Serilog file sink is used (the integration test sink, and any optional persistent local-dev sink), its file path and rotation policy MUST be configurable via environment variables to accommodate different local and CI environments.
- **FR-022**: All telemetry transport — from the Angular frontend to the backend proxy, and from the .NET backend (including the proxy) to the local dashboard or any production vendor — MUST use OTLP over HTTP/protobuf. gRPC-based OTLP transport is explicitly excluded from this system. The .NET OTLP exporter defaults to gRPC, so the protocol MUST be explicitly set to `http/protobuf` (e.g., `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`) and the exporter MUST target the dashboard's HTTP/protobuf OTLP endpoint (the .NET Aspire Dashboard exposes this on port 18890, distinct from its gRPC endpoint on 18889), using the `/v1/traces`, `/v1/logs`, and `/v1/metrics` paths.
- **FR-023**: OpenTelemetry MUST be the single owner of the distributed activity pipeline in the .NET backend. Sentry MUST be configured to consume OpenTelemetry activities rather than run its own independent tracer, eliminating the risk of double-sampling, span duplication, or conflicting hooks on the shared activity pipeline. Sentry's error-capture, release-tracking, and environment-tagging capabilities MUST continue to function through this unified pipeline. Sentry MUST apply its OWN configurable trace sample rate (defaulting to the current behavior of approximately 20%) on top of the activities it consumes, so that Sentry's trace volume — and therefore quota/cost — is decoupled from the OTel sampler's 100% default (FR-027). This Sentry-side rate MUST be configurable via environment variable with no source code change. Exception/error capture is NOT subject to this trace sample rate — all captured errors are reported regardless of trace sampling.
- **FR-024**: In the integration test environment, the .NET backend MUST be configured with a Serilog JSON sink (file or in-memory) as the log output. Integration tests MUST assert on log entries from this sink. The sink MUST be a real Serilog sink, not a mock or test double.
- **FR-025**: In the integration test environment, trace and metric assertions MUST use OTel in-memory exporters registered through the test host's service configuration. No external telemetry service (dashboard or collector container) may be a dependency for any integration test.
- **FR-026**: *(Removed — the OTLP egress smoke test targeted the collector, which no longer exists. The backend's OTLP exporter configuration is covered by the in-memory exporter assertions of FR-025; live egress to the Aspire Dashboard/vendor is verified manually per the Quality Gates.)*
- **FR-027**: The telemetry pipeline MUST apply a configurable sampling strategy for traces, controllable via environment variable. The OTel sampler (governing export to the Aspire Dashboard / primary vendor) MUST default to capturing 100% of traces in BOTH development and production, so that no traces are silently lost by default. Operators MUST be able to reduce the sampling rate (e.g., under load or vendor-cost pressure) via the environment variable alone, with no source code modification. Sampling MUST be head-based and propagated across the frontend→backend boundary so a sampled trace is captured end to end. Sentry MUST sample independently of the OTel sampler — see FR-023.
- **FR-028**: Cross-origin trace-context propagation MUST be enabled end to end. The backend API's CORS policy MUST permit the W3C `traceparent` (and `tracestate`) request headers so browser-originated API requests can carry trace context to the backend. Because browser telemetry export is routed through the same-origin backend proxy (FR-016), no cross-origin OTLP receiver configuration is required for the telemetry-export path.
- **FR-029**: The frontend telemetry exporter target MUST be environment-configurable (it points at the same-origin backend proxy of FR-016). Vendor selection for production remains deferred to the deployment-planning phase; the backend proxy attaches vendor credentials server-side per FR-016, so no vendor API keys or credentials ever appear in browser-accessible code in any environment.
- **FR-030**: The Angular frontend MUST use the OpenTelemetry JavaScript Web SDK (`@opentelemetry/sdk-trace-web` with the OTLP/HTTP trace exporter) for instrumentation. It MUST use `instrumentation-document-load` and `instrumentation-xml-http-request` auto-instrumentation together with `ZoneContextManager` (the app's `HttpClient` is XHR-based and the app uses Zone.js). The frontend MUST emit traces only — document-load spans, XHR/HttpClient request spans, and recorded JS errors — and MUST NOT emit browser logs or browser metrics. SDK initialization MUST occur during app bootstrap (e.g., an `APP_INITIALIZER` or equivalent in `app.config.ts`) and MUST NOT throw on valid configuration nor block application startup if the exporter target is unavailable. The frontend OTLP trace exporter URL MUST point at the telemetry proxy endpoint of FR-031 and MUST be resolvable from the existing `environment.*.ts` configuration (a relative path in the nginx-served Docker stack; an absolute API-origin URL for `ng serve` and production).

- **FR-031**: The backend MUST expose a telemetry proxy endpoint — a single additive route that receives browser trace telemetry and forwards it to the configured destination. This endpoint is the only new API surface introduced by this feature. It MUST satisfy:
  - **Route & method**: `POST` to a configurable path (default `/api/v1/telemetry`) on the existing API; the frontend OTLP exporter (FR-030) targets this URL.
  - **Payload**: accepts OTLP/HTTP protobuf trace payloads (per FR-022) and enforces a configurable maximum request body size (default 1 MB), rejecting larger requests with HTTP 413.
  - **Passthrough forwarding**: forwards the OTLP payload to the configured OTLP destination (Aspire Dashboard locally / vendor in production) WITHOUT regenerating spans, preserving the browser-supplied trace and span identifiers so frontend and backend spans correlate into one end-to-end trace. The destination endpoint and auth headers come from the same environment configuration as the backend exporter (FR-007), so switching dashboard↔vendor requires no code change.
  - **Credentials**: any vendor credentials are attached server-side only and MUST NOT appear in browser-accessible code. The destination MUST be server-configured and MUST NEVER be specified by the client (the endpoint MUST NOT act as an open relay to arbitrary targets).
  - **Scrubbing**: the FR-009 scrubbing policy MUST be applied to forwarded browser telemetry.
  - **Auth posture**: the endpoint MUST permit anonymous (unauthenticated) requests so telemetry from pre-login pages (login, registration) is captured; it MUST NOT require a JWT. When a valid JWT is present, the authenticated user's subject GUID MAY be attached as a span attribute (per FR-011 — never email or other PII).
  - **Abuse controls**: the endpoint MUST be rate-limited (mirroring the existing fixed-window limiters, keyed by client IP) in addition to the body-size cap.
  - **Response & failure semantics**: the endpoint MUST acknowledge quickly (HTTP 202 or 204) and forward asynchronously (fire-and-forget); forwarding failures MUST be swallowed per FR-008 and MUST NOT surface an error to the browser or affect the page.
  - **CORS**: the endpoint is served by the application's own API. It is same-origin in the nginx-served Docker stack; when the SPA is served from a different origin (`ng serve`, or the production `api.` subdomain) the request is cross-origin and MUST be permitted by the API's existing CORS policy, including the OTLP content type and the `traceparent` header (per FR-028).

### Key Entities

- **Trace**: A complete record of a single user-initiated operation spanning from browser action to database response, composed of one or more ordered spans linked by a shared trace identifier.
- **Span**: An individual unit of work within a trace — such as an API handler execution, a database query, or a frontend HTTP call — with a start time, duration, and optional key-value attributes.
- **Log Entry**: A structured, machine-readable event record enriched with trace identifier, span identifier, user identity, and environment metadata.
- **Metric**: A numeric measurement aggregated over time — such as request count, error rate, or response duration distribution — used for dashboards and alerting.
- **Telemetry Proxy**: An additive backend endpoint (`POST /api/v1/telemetry` by default; see FR-031) that receives browser-originated trace telemetry over OTLP/HTTP and forwards it as a passthrough to the configured telemetry destination, attaching destination credentials server-side and preserving the browser's trace/span identifiers. Permits anonymous requests, is rate-limited and body-size-capped, and never lets the client choose the destination. Exists so the frontend never holds vendor credentials and so the local and production frontend telemetry paths are identical.
- **Telemetry Destination**: The configured target that receives traces, logs, and metrics via OTLP/HTTP — either the local .NET Aspire Dashboard or a cloud observability vendor endpoint. The .NET backend exports to it directly; browser telemetry reaches it via the Telemetry Proxy.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Telemetry data must not include HOA-internal identifiers (e.g., HOA IDs, community IDs) as trace attributes in shared cloud dashboards, to avoid inadvertent cross-tenant data exposure. Service-level trace context (request path, duration, status code) is acceptable.
- **Authorization**: The local development dashboard is unauthenticated and developer-local only — it must not be exposed beyond the developer's machine. Production observability vendor access is governed by that vendor's access controls; no application-level authentication layer is added to the telemetry pipeline.
- **Observability**: This feature extends the existing observability infrastructure using a unified pipeline. OpenTelemetry owns the single distributed activity pipeline in the .NET backend; Sentry subscribes to it as a consumer (Sentry-on-OTel) rather than running a parallel tracer — eliminating double-sampling and span duplication. Sentry retains full error-capture, release-tracking, and environment-tagging functionality through this arrangement. Serilog continues to own structured log output and MUST be configured to emit logs in structured JSON format via OTLP to the configured destination; trace context must be injected into the existing Serilog enrichment pipeline so every log entry carries the active trace and span identifiers. In local development, the telemetry pipeline is: .NET backend → Aspire Dashboard (direct OTLP), and browser → same-origin backend telemetry proxy → Aspire Dashboard. There is no separate collector intermediary; the collector is re-introducible later via environment variable if tail-sampling or multi-vendor fan-out is ever required. All legs of this pipeline use OTLP over HTTP/protobuf; gRPC is not used anywhere in the telemetry path. In the integration test environment, the backend runs in-process via WebApplicationFactory; log assertions use a Serilog JSON sink configured for the Test environment, and trace/metric assertions use OTel in-memory exporters — no external telemetry service is in the test path. The new pipeline adds distributed tracing, metrics, and the local dashboard on top of these existing capabilities without replacing them.
- **Security and abuse controls**: Trace attributes and log fields must be scrubbed of all sensitive data at the SDK/exporter level: passwords, tokens, card numbers, account numbers, email addresses, and full names. This scrubbing applies to both local and production telemetry destinations.
- **Database/runtime**: SQL query text capture must be gated by an environment variable that defaults to enabled in development and disabled in production. No code change is required to toggle this behavior between environments.
- **API contract**: No changes to existing API response shapes or error formats. Trace identifiers must not appear in API response bodies — they are internal observability signals only. One additive endpoint is introduced — the telemetry proxy (`POST /api/v1/telemetry`, FR-031) — which accepts OTLP/HTTP trace payloads and returns 202/204 with no response body; it does not alter any existing contract. It permits anonymous access, is rate-limited and body-size-capped, and forwards only to a server-configured destination (never a client-specified one).
- **Quality gates**: Telemetry initialization in both Angular and .NET must have test coverage verifying: the provider initializes without throwing on valid configuration, and trace context headers are present on outbound HTTP requests (Angular). In the .NET integration test suite: log assertions read from a real Serilog JSON sink (file or in-memory) configured for the Test environment; trace and span assertions (e.g., SQL query text in FR-004, trace ID propagation in FR-002) use OTel in-memory exporters registered through WebApplicationFactory. No external telemetry service (dashboard or collector container) is started by the test suite; live OTLP egress to the Aspire Dashboard is verified manually during local development.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can identify the root cause of a failed API request — including which handler or database query caused the failure — within 5 minutes using the local dashboard alone, without additional tools.
- **SC-002**: Every browser-initiated action that calls the backend produces a single end-to-end trace visible in the local dashboard within 30 seconds of the action completing.
- **SC-003**: 100% of backend log entries generated during an in-flight request include the trace identifier of that request.
- **SC-004**: Switching the telemetry destination from the local dashboard to any production cloud vendor requires updating 3 or fewer environment variables and zero source code changes.
- **SC-005**: Database query text is visible within traces for 100% of database operations performed in the development environment.
- **SC-006**: Telemetry pipeline failures (dashboard or vendor unreachable) produce zero increase in API error rate and no more than a 2% increase in API p95 response time versus a telemetry-disabled baseline.
- **SC-007**: A developer can access a working local observability dashboard within 5 minutes of running the application stack for the first time, with no documentation required beyond the standard application startup instructions.
- **SC-008**: Integration tests can assert on structured log output using a real Serilog JSON sink (file or in-memory) configured for the Test environment; no mocking or stubbing of the logging framework is required. A test double is not acceptable; a real sink is required.
- **SC-009**: All log entries produced by the real Serilog sink (in tests) and emitted to the Aspire Dashboard (in local dev) are valid JSON and include at minimum: timestamp, severity level, message, trace identifier, and span identifier for any entry emitted during an in-flight request.

## Assumptions

- The existing Serilog structured logging is preserved and extended with trace context enrichment and JSON output — not replaced.
- Sentry error-capture capability is fully preserved. Sentry is reconfigured to consume the OpenTelemetry activity pipeline (Sentry-on-OTel) rather than running its own tracer; this is a configuration change, not a removal. All existing Sentry behavior — exception capture, release tracking, environment tagging, PII scrubbing — continues unchanged.
- The local development observability dashboard (.NET Aspire Dashboard) runs as part of the local Docker Compose stack; it is not deployed to any shared or production environment.
- The .NET Aspire Dashboard is added to the existing Docker Compose configuration as a new service alongside the existing .NET backend, Angular frontend, and Postgres services. No separate telemetry-collector service is added.
- SQL query text capture is enabled by default in development and disabled by default in production, controlled via an environment variable.
- Frontend telemetry routes through a same-origin backend proxy endpoint in BOTH local development and production, so the browser never holds vendor credentials and the path is identical across environments. Production vendor selection is deferred to the deployment planning phase.
- The `neko-hoa-mock` application is out of scope for this feature; instrumentation targets only the `HOAManagementCompany` API and `neko-hoa` Angular frontend.
- The `neko-hoa` frontend is Angular 17.3 (standalone bootstrap, functional HTTP interceptors, XHR-based `HttpClient`, Zone.js). The frontend OTel SDK and instrumentation choices in FR-030 follow from this. The frontend emits traces only; the OTLP exporter posts to a same-origin relative path served by the backend proxy (FR-016), so no browser-side environment variable is required for the exporter endpoint.
- All instrumentation is added at the infrastructure and middleware level. Existing HOA domain features (payments, communities, violations, properties) do not require modification.
- Integration tests run the backend in-process via WebApplicationFactory with Testcontainers supplying Postgres and MinIO (matching the existing test harness). No external telemetry service is in the test path. Log assertions use a Serilog JSON sink (file or in-memory) configured for the Test environment; trace and metric assertions use OTel in-memory exporters.
- The Serilog test sink is a real sink, not a mock. The "no test double" constraint in SC-008 applies to the logging framework itself; using a real in-memory sink is compliant.
- All OTLP telemetry transport uses HTTP/protobuf exclusively. The primary driver is browser compatibility — web browsers cannot initiate gRPC connections natively, so the Angular frontend requires HTTP. Using the same protocol for the backend leg and the proxy leg keeps the configuration uniform.
- The OTel Collector is intentionally omitted from this feature. Its remaining justifications (test log file, browser CORS endpoint) are satisfied by the Serilog test sink and the same-origin backend proxy respectively. Because all transport is OTLP, a collector can be inserted later — for tail-based sampling or multi-vendor fan-out — by repointing the exporter endpoint environment variable, with no application code change.
- Performance overhead of the telemetry pipeline in production is assumed to be within standard industry benchmarks (under 1% throughput impact at moderate load) and will be validated during implementation.

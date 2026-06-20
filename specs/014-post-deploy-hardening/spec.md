# Feature Specification: Hardening not addressed by ephemeral environments

**Feature Branch**: `014-post-deploy-hardening`  
**Created**: 2026-06-20  
**Status**: Draft  
**Input**: User description: "Address the defects/risks that per-PR ephemeral environments would NOT fix: (1) the global API rate limiters, (2) the post-deploy smoke suite running the entire local e2e suite instead of a curated subset, and (3) remaining `IsDevelopment()`-style environment-name gating that silently no-ops in the deployed `Dev` environment."

## Why this feature exists *(context for a fresh reader)*

This product is a .NET API (FastEndpoints) plus an Angular frontend, deployed to Google Cloud Run behind Cloudflare, with Cloudflare R2 for object storage and Neon (PostgreSQL) for data. A separate effort introduces ephemeral per-PR test environments to catch real-infrastructure issues before merge. The three concerns below are **orthogonal** to that effort — an isolated per-PR environment does not surface or resolve them — so they are scoped here independently. Each is an independently deliverable slice.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - API rate limiting protects per-client, not globally (Priority: P1)

As a legitimate end user (and as the operator protecting the service), I need the authentication and payment rate limits to throttle an individual abusive client without throttling the entire user base, so that normal activity — including routine token refreshes — is never rejected because of someone else's traffic.

**Current problem (observed):** In `HOAManagementCompany/Program.cs` the limiters are registered with `AddFixedWindowLimiter("auth", …)` (permit limit 10/min) and `AddFixedWindowLimiter("payments", …)` (permit limit 20/min). These named limiters have **no partition key**, so all clients share a single global bucket: the entire user base is collectively capped at 10 auth requests (login **and** token refresh combined) per minute and 20 payment requests per minute. At trivial real traffic this throttles legitimate users (token refreshes alone exhaust it) — effectively a self-inflicted denial of service. The `telemetry` limiter in the same block already uses the partitioned pattern (`AddPolicy` + `RateLimitPartition.GetFixedWindowLimiter(...)`), but partitions on `httpContext.Connection.RemoteIpAddress`, which behind Cloud Run + Cloudflare is the proxy/load-balancer address — so even that is effectively global. With no forwarded-headers handling configured anywhere in the codebase, no limiter can currently see the true client. A correct fix has two parts: (a) resolve the true client identity from the trusted edge (e.g., the Cloudflare client-IP header or a correctly configured forwarded-for chain), and (b) partition the auth and payment limiters by that resolved identity.

**Why this priority**: It is a live production fault affecting real end users right now, independent of any test tooling.

**Independent Test**: Can be fully tested by issuing concurrent auth/payment requests from multiple simulated client identities and confirming that one client exhausting its quota does not cause rejections for the others, that the partition is keyed on the resolved client identity, and that a forged forwarded header from an untrusted source cannot reassign or evade a client's quota.

**Acceptance Scenarios**:

1. **Given** many distinct clients, **When** each makes a normal volume of auth/payment requests, **Then** no client is throttled due to another client's traffic.
2. **Given** a single abusive client exceeding its limit, **When** it continues, **Then** only that client receives rate-limit rejections.
3. **Given** the app is behind the production edge, **When** the limiter partitions requests, **Then** it partitions by the resolved true client identity, not the shared proxy address.
4. **Given** a spoofed forwarded-IP header from an untrusted source, **When** a request arrives, **Then** the client identity cannot be forged to evade or redirect limits.

---

### User Story 2 - Post-deploy smoke gate is a curated, deterministic subset (Priority: P2)

As an engineer relying on the post-deploy gate, I need it to run a small, fast, deterministic, read-mostly set of checks that answer "is this deployment healthy," rather than the full local end-to-end regression suite, so that the gate is trustworthy and does not pollute the shared environment.

**Current problem (observed):** The frontend script `e2e:playwright-dev` (in `neko-hoa/package.json`) is `playwright test --grep-invert @local-only` — it runs **every** Playwright spec under `neko-hoa/e2e/` (auth, community, dashboard, payments, property) against the deployed environment. Many of these were authored for local runs and assume local conditions, and several **mutate shared state**: registration claims a property; turning off auto-pay disables the seed user's enrollment and, because seeding is idempotent, it stays off; poll-vote and RSVP write data. The result is data-dependency flakes, ordering/rate-limit flakes under parallel load, and persistent side effects on the shared environment.

**Why this priority**: It is the structural reason a long sequence of unrelated test failures surfaced only post-deploy; curating the gate prevents recurrence and removes shared-state pollution. Lower priority than US1 because it does not affect end users directly.

**Independent Test**: Can be fully tested by running the curated smoke gate against a shared environment, confirming it executes only the designated deployment-health checks, leaves no persistent mutations afterward, returns deterministic results across repeated runs, and still fails when a genuine deployment break is injected — while the full regression suite remains separately runnable.

**Acceptance Scenarios**:

1. **Given** the smoke gate runs, **When** it executes, **Then** it runs only checks designated as smoke (deployment-health) checks, not the full local regression suite.
2. **Given** the smoke gate runs against a shared environment, **When** it completes, **Then** it has not left persistent mutations (no created accounts, no toggled enrollment, no reliance on test-only cleanup endpoints).
3. **Given** the smoke gate runs repeatedly, **When** environment data varies within expected bounds, **Then** results are deterministic (no data-dependent flakes).
4. **Given** the full regression suite, **When** developers run it locally or on PRs, **Then** it remains available and runnable outside the smoke gate.
5. **Given** a genuine deployment break (auth down, key pages not rendering, API unreachable), **When** the smoke gate runs, **Then** it fails loudly.

---

### User Story 3 - Environment behavior is config-gated, not host-name-gated (Priority: P3)

As an engineer debugging the deployed `Dev` environment, I need environment-specific debugging behavior to be driven by explicit configuration flags rather than host-environment-name checks, so that intended `Dev` debuggability is actually present in `Dev` and never silently no-ops.

**Current problem (observed):** The deployed development environment runs as `ASPNETCORE_ENVIRONMENT=Dev`, but code gated on `env.IsDevelopment()` is true only for `Development`. This pattern has already caused functional defects (document seeding and a test-cleanup endpoint silently doing nothing in `Dev`, since fixed via config flags). Remaining instances that reduce `Dev` debuggability:

- `HOAManagementCompany/Features/Common/GlobalExceptionHandler.cs` returns full exception detail only when `IsDevelopment()`, so the deployed `Dev` environment returns no detail.
- `HOAManagementCompany/Infrastructure/Observability/ObservabilityOptions.cs` enables `CaptureSqlText` only when `IsDevelopment()`, so `Dev` traces omit SQL text.

The project already uses the right pattern elsewhere (config flags such as `Startup:*` and `DevTools:*`).

**Why this priority**: These are debuggability gaps, not functional or user-facing failures — lowest impact, but cheap to fix and worth eliminating the foot-gun class entirely.

**Independent Test**: Can be fully tested by evaluating each environment-conditional behavior under a `Dev`-style host with the enabling flag set (behavior present), under production defaults (behavior absent), and by auditing the codebase for remaining host-name gates that should also apply to `Dev`.

**Acceptance Scenarios**:

1. **Given** the deployed `Dev` environment, **When** an environment-specific debugging behavior is intended for `Dev`, **Then** it is enabled there via configuration (not skipped because the host name is not `Development`).
2. **Given** the production environment, **When** the same behaviors are evaluated, **Then** they remain disabled by default (no sensitive detail leakage).
3. **Given** a code audit, **When** searching for host-environment-name gates that should also apply to `Dev`, **Then** none remain that silently no-op in `Dev`.

### Edge Cases

- A burst of legitimate traffic from many users behind a single corporate NAT (shared client IP) — limits must be tuned to avoid false positives while still curbing abuse.
- Forwarded-header trust: only the known edge proxy may set the client-identity header; direct or forged values from untrusted sources must be rejected or ignored.
- A request that arrives without a resolvable trusted client identity (e.g., missing the edge header) must still be handled safely without granting an unbounded or shared bucket that re-creates the global-throttle fault.
- Exposing exception detail or SQL text in any environment must never leak secrets or PII; "Dev" enablement must still respect sensitive-data exclusions.
- The smoke subset must still fail loudly on genuine deployment breakage (auth down, key pages not rendering, API unreachable) — curation must not hollow out the gate.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Auth and payment rate limits MUST be enforced per individual client identity, not as a single global bucket shared by all clients.
- **FR-002**: The system MUST derive client identity for rate limiting from the trusted edge proxy's forwarded client address, and MUST reject or ignore forwarded-identity values originating from untrusted sources.
- **FR-003**: A single client exceeding a limit MUST NOT cause rate-limit rejections for other clients.
- **FR-004**: Rate-limit thresholds MUST be configurable per environment without code changes.
- **FR-005**: The post-deploy smoke gate MUST execute only a designated subset of checks oriented to deployment health, excluding state-mutating and local-only tests.
- **FR-006**: The smoke gate MUST NOT leave persistent side effects on the environment it runs against, and MUST NOT require test-only data-cleanup endpoints to remain correct across runs.
- **FR-007**: The full end-to-end regression suite MUST remain runnable for local/PR use, separate from the smoke gate.
- **FR-008**: Environment-conditional behaviors MUST be driven by explicit configuration flags, defaulting to safe/off, rather than host-environment-name checks; and MUST evaluate correctly in the deployed `Dev` environment.
- **FR-009**: No environment-conditional debugging output (exception detail, query text) may expose secrets or personal data in any environment.

### Key Entities *(include if feature involves data)*

- **Rate-limit partition key**: the per-client identity used to bucket auth/payment requests, resolved from the trusted edge.
- **Smoke check set**: the curated collection of deployment-health checks, distinct from the full regression suite.
- **Environment feature flag**: a configuration value that enables or disables an environment-conditional behavior independent of the host environment name.

### Constitution Requirements *(mandatory when applicable)*

- **Authorization**: No change to the authorization model; rate limiting applies ahead of/independent of authorization and MUST NOT weaken existing server-side protected-action checks.
- **API contract**: Rate-limit rejection responses retain the existing error shape and status semantics; no breaking change to success responses. Exception-detail exposure changes only the `Detail` field content under explicit `Dev` configuration, never the response contract.
- **API implementation and docs**: Limiters remain wired through the existing FastEndpoints pipeline; `/swagger` exposure rules are unchanged (development-only, disabled in production).
- **Security and abuse controls**: Rate limits MUST be per-client and resistant to forged forwarded headers; the client-identity header MUST be trusted only from the known edge proxy. Debugging surfaces (exception detail, SQL text) MUST default off and exclude sensitive data; production posture MUST NOT regress.
- **Observability**: SQL-text capture and exception detail are environment-gated by explicit config; any enablement respects sensitive-data exclusions and is visible/auditable. Trace/error tagging by environment and release is preserved.
- **Quality gates**: Per-client rate limiting, the curated smoke set, and config-gated behaviors MUST each be covered by automated tests — including forged-header rejection and a "one client does not throttle another" test — and these tests MUST remain safe under parallel execution.
- **Frontend testing**: The curated smoke set is selected via tagging (or an equivalent mechanism) within the existing Playwright suite; the full Cypress/Playwright regression coverage remains intact for local/PR runs.
- **Executable & living spec**: Every mandatory acceptance scenario and functional requirement maps to an automated test that can be run on demand and currently passes; this `spec.md` stays in sync with the code (drift fixed before merge); this feature's `spec.md` and `tasks.md` are updated before the PR; any direct contradiction with a former spec is reconciled so the spec corpus stays internally consistent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Under normal multi-user traffic, the rate of legitimate requests rejected due to *another* client's activity is 0%.
- **SC-002**: An abusive single client is limited within its configured threshold while other clients are unaffected (demonstrated by an automated test).
- **SC-003**: A forged client-identity header from an untrusted source cannot change which client a request is attributed to (demonstrated by an automated test).
- **SC-004**: The smoke gate completes in a fraction of the full suite's runtime and runs only deployment-health checks (0 state-mutating tests in the gate).
- **SC-005**: Running the smoke gate any number of times against the same environment leaves the data it owns equivalent before and after (no persistent mutations).
- **SC-006**: A repository audit finds 0 remaining host-environment-name gates that should apply to `Dev` but silently no-op there.
- **SC-007**: No regression in production security posture: exception detail and query text remain disabled in production by default.

## Assumptions

- The production edge is Cloudflare in front of Cloud Run; the trusted client-identity source is the edge's forwarded header. The exact header and trusted-proxy configuration are confirmed during planning.
- Rate-limit thresholds are set conservatively for production and may be raised for non-production environments via configuration.
- The smoke subset is defined by tagging (or an equivalent selection mechanism) the deployment-health-appropriate checks; the full suite continues to exist unchanged for local/PR runs.
- "Dev" debugging enablement (exception detail, SQL text) is acceptable from a data-sensitivity standpoint for a non-production environment, provided secret/PII exclusions still apply.
- These three items are independent and may be delivered as separate slices, with US1 first as the only end-user-facing fault.

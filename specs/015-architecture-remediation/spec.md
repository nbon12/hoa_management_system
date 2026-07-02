# Feature Specification: Architecture Remediation — Proper Target Architecture

**Feature Branch**: `015-architecture-remediation`
**Created**: 2026-07-01
**Status**: Draft
**Input**: User description: "incorporate these findings into a specification that outlines the proper architecture"

**Source findings**: `docs/architecture-smells-analysis.md` (architecture smells analysis of 2026-07-01, 20 findings ranked by severity). This specification turns those findings into a target architecture and a prioritized, independently deliverable remediation program. Finding numbers referenced below (F1–F20) are the numbered findings in that report.

## Clarifications

### Session 2026-07-02

> Interactive clarification was unavailable in this session (question tool could not reach the user), so the recommended default was adopted for each item below and integrated into the spec. Any of these can be overridden by answering differently and re-running `/speckit.clarify`.

- Q: What delivery scope is committed? → A: The full P1–P6 program, delivered as staged vertical slices in priority order; descoping P5/P6 requires an explicit later decision that amends this spec.
- Q: Is the payment-provider SDK leak into feature code (report finding F11 — raw provider event types consumed by webhook/reconciliation logic) in scope? → A: Yes — in scope under P5 as a layering rule: provider-SDK types are confined to the gateway adapter, and inbound events get a gateway-neutral representation (new FR-021).
- Q: Does client contract generation produce types only, or a full generated API client? → A: Types only. Request/response shapes are generated from the server's API description; the hand-written client services and their mapping/anti-corruption code remain, now consuming generated types (FR-011).
- Q: How is historical status/ledger inconsistency detection (FR-005) operated? → A: Report-only detection runs once at cutover and then recurringly on the existing reconciliation cadence, surfacing findings through logs/alerts; no automated correction.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Payment records are always correct, even when processing is interrupted (Priority: P1)

A homeowner's payment (card, ACH, refund, or reversal) is reported to the platform by the payment provider. Regardless of when the system crashes, restarts, or retries that report, the homeowner's account ledger reflects the event exactly once: one payment entry, one reversal, one insufficient-funds fee, one refund. The homeowner never sees a duplicated charge, fee, or refund on their statement, and the recorded payment status always agrees with the ledger.

**Why this priority**: This is the only finding class that can silently corrupt homeowner financial data (F9: non-atomic webhook writes + retry loop → double-posted reversals/NSF fees/refunds). Financial correctness outranks all structural concerns.

**Independent Test**: Can be fully tested by simulating a payment-provider event whose processing is interrupted partway (after the ledger write, before the status update), letting the system's retry mechanism re-process it, and verifying the ledger contains exactly one entry per business event and the payment status matches the ledger.

**Acceptance Scenarios**:

1. **Given** a payment-provider event (success, failure, ACH return, refund, or dispute) is being processed, **When** processing fails at any intermediate point and the event is later retried, **Then** the resulting ledger contains exactly one entry per business event and no orphaned or duplicated entries.
2. **Given** an ACH return event, **When** it is processed to completion, **Then** the reversal entry, the insufficient-funds fee, and the payment-status change become visible together (all-or-nothing) — an observer can never read a state where one exists without the others.
3. **Given** a refund event already fully applied, **When** the same event is re-delivered or retried, **Then** no additional refund entry is created and the cumulative refunded amount is unchanged.
4. **Given** a balance recomputation runs while a new ledger entry is being appended for the same property, **When** both complete, **Then** the resulting running balances are consistent with all entries (no lost update).
5. **Given** the three payment-recording flows (one-time checkout, recurring draft, provider-event settlement), **When** each records a settled payment, **Then** all three produce identical record structure and identical atomicity guarantees via one shared recording path (removes the triplication in F10).

---

### User Story 2 - Consistent, predictable error responses across the whole API (Priority: P2)

A resident or staff member who triggers a business error anywhere in the product (wrong credentials, accessing another property, voting twice in a poll, downloading a restricted document) always receives the same well-formed error response: a stable error code, a human-readable message, and the correct status — never a generic "something went wrong" for a known business condition, and never an internal server error caused by a missing session attribute.

**Why this priority**: Today business-error mapping is hand-written in 12 endpoints, and endpoints that omit it surface business errors as internal server errors (F5); identity attributes are parsed inline in 24 places and a missing claim crashes the request. This directly shapes what every user sees when something goes wrong.

**Independent Test**: Can be tested by exercising each documented business-error condition through the public API and asserting a uniform error envelope (code, message, status), plus one test where the required identity attribute is absent and the response is a clean authorization error rather than a server fault.

**Acceptance Scenarios**:

1. **Given** any endpoint that can raise a known business error, **When** the error occurs, **Then** the response carries the error's code, message, and intended status through one central mapping — with zero per-endpoint hand-written error translation.
2. **Given** a request whose authenticated session lacks the required property/identity attribute, **When** any protected endpoint handles it, **Then** the caller receives a clean authorization failure (not an internal server error).
3. **Given** a new endpoint is added without any error-handling boilerplate, **When** it raises a business error, **Then** the central mapping still produces the uniform envelope (fail-safe by default).

---

### User Story 3 - Production cannot run test machinery (Priority: P3)

An operator deploying to production has a guarantee — enforced by the platform, not by configuration discipline — that test-support machinery (database seeding, sample test files, destructive test-cleanup endpoints) can never execute in the production environment, even if a configuration flag is set by mistake.

**Why this priority**: Today an anonymous destructive endpoint, seeders, and test data ship in the production binary guarded only by config flags whose "off in production" invariant is a comment (F6). Risk is severe but requires a misconfiguration to materialize, so it ranks below active financial correctness and user-visible error behavior.

**Independent Test**: Can be tested by starting the application in the production environment configuration with all test-support flags deliberately enabled, then verifying the cleanup endpoint behaves as if it does not exist, no seeding occurs, and no test artifacts are present in the deployed output.

**Acceptance Scenarios**:

1. **Given** the application runs in the production environment, **When** the test-cleanup endpoint is invoked with its enabling flag set, **Then** the request is refused as if the endpoint did not exist, and the refusal is logged as a security-relevant event.
2. **Given** the application starts in the production environment with data-seeding enabled in configuration, **Then** seeding does not run and the conflict is surfaced at startup.
3. **Given** a production build artifact, **When** its contents are inspected, **Then** no sample/test data files are included (F6).

---

### User Story 4 - One source of truth for the client–server contract (Priority: P4)

A developer changes or adds an API response shape on the server. The web client's types for that shape are regenerated from the server's published API description rather than re-typed by hand, so the client cannot silently drift from the server. Stale, unused duplicate type definitions are gone.

**Why this priority**: The contract is currently maintained three times by hand and has already drifted — dead types in the shared model barrel superseded by parallel service-local definitions, plus hand-written story fixtures (F4, F13, F16). Drift causes real user-facing defects but is a slower-burning risk than P1–P3.

**Independent Test**: Can be tested by introducing a deliberate mismatch between a server response shape and the client's generated types and confirming the verification pipeline fails; and by verifying the client contains exactly one definition per contract type, with the previously identified dead types removed.

**Acceptance Scenarios**:

1. **Given** the server publishes its API description, **When** client contract types are produced, **Then** they are generated from that description, and regeneration is repeatable on demand.
2. **Given** a server-side contract change without regenerating client types, **When** the delivery pipeline runs, **Then** the drift is detected and fails verification before release.
3. **Given** the client codebase, **When** searched for contract-type definitions, **Then** each API concept has exactly one definition (the dead `RecurringPayment`/`DraftEntry`-style duplicates and parallel story-fixture shapes no longer exist).

---

### User Story 5 - The codebase's layers match the intended architecture (Priority: P5)

A developer opening the codebase finds the dependency arrows pointing the intended way: the domain layer owns shared business concepts (including the business-error type), infrastructure depends only on domain/shared abstractions (never on feature slices), feature slices do not import other features' internals, and cross-cutting policies (identity parsing, money rounding/currency, scheduled-job authentication) exist in exactly one place. Dead artifacts (unused build files, empty scaffolding directories) are gone.

**Why this priority**: These violations (F2, F3, F8, F11, F12, F15) don't change today's runtime behavior, but every future feature pays their tax; fixing them is what makes the P1–P4 guarantees durable.

**Independent Test**: Can be tested by automated dependency checks: no infrastructure module references a feature module; no feature imports another feature purely for shared kernel types; each named cross-cutting policy has a single definition site.

**Acceptance Scenarios**:

1. **Given** the backend solution, **When** dependencies are analyzed, **Then** zero infrastructure files reference feature namespaces (today: 8), and configuration/option types consumed by infrastructure live in a shared location.
2. **Given** the business-error type, **When** its location and importers are inspected, **Then** it lives in the domain layer and no feature imports the authentication feature solely to use it (today: 11+ files).
3. **Given** the repository, **When** inspected, **Then** the orphaned root container build file and the empty UI-scaffold directory are removed, and exactly one authoritative container build definition remains (F15, F8).
4. **Given** monetary conversions and rounding, **When** searched, **Then** the minor-unit conversion, rounding policy, and currency designation each have one shared definition (F12).
5. **Given** the payment-provider integration, **When** dependencies are analyzed, **Then** provider-SDK types appear only inside the gateway adapter (today: 3 feature files consume raw provider event types), and event handlers are unit-testable against the gateway-neutral event representation (F11).

---

### User Story 6 - Fast, layered feedback for developers and a delivery pipeline that separates verification from release (Priority: P6)

A developer can run the pure unit-test tier in seconds without any containers or external services. The delivery pipeline separates "verify this change" (pull-request feedback) from "release this change" (build, publish, deploy, promote), so a CI change cannot accidentally alter the release path. Duplicated infrastructure definitions for ephemeral and long-lived environments share a common core. The two largest payment screens are decomposed so their pieces can be understood and tested in isolation.

**Why this priority**: Developer-experience and delivery-structure findings (F7, F13, F14, F17–F20) compound over time but have no direct user-facing failure mode; they are the long tail of the remediation.

**Independent Test**: Can be tested by running the unit tier with container tooling unavailable and observing success; by verifying the pull-request pipeline and release pipeline are separate, independently runnable definitions; and by confirming the two environment infrastructure modules consume a shared core module.

**Acceptance Scenarios**:

1. **Given** a machine without container tooling, **When** the unit-test tier runs, **Then** it completes successfully (integration tiers remain container-based and separately invocable).
2. **Given** the delivery configuration, **When** inspected, **Then** pull-request verification and release/deploy are separate pipelines, and repeated setup steps are factored into shared reusable steps.
3. **Given** the infrastructure modules for standard and ephemeral environments, **When** compared, **Then** the container-service, secrets, and database-branch definitions come from one shared module rather than two divergent copies.
4. **Given** the two largest payment screens, **When** their structure is reviewed, **Then** presentation, form/wizard state, payment-element handling, and API orchestration are separated and independently testable, and error presentation comes from a central mechanism rather than per-screen handlers.

---

### Edge Cases

- What happens when a payment-provider event is delivered twice concurrently (two workers pick up the same event)? The per-property serialization and event-level dedupe must together still yield exactly-once ledger effects.
- What happens when the retry mechanism re-processes an event whose payment is already in a terminal state — including partially applied historical events created before this remediation? The handler must be a safe no-op.
- How does the system behave when the production environment identity is ambiguous (e.g., environment name unset) and test-support flags are enabled? The safe default must be "test machinery disabled".
- What happens to client type generation when the server API description is temporarily unavailable or invalid? The pipeline must fail visibly rather than fall back to stale hand-written types.
- What happens to data that was double-posted before the fix (if any exists)? Remediation must detect and report historical inconsistencies between payment status and ledger contents, not silently mutate them.
- During the migration window, what happens if some flows use the shared payment-recording path while others still use the legacy copies? Each flow is cut over whole; mixed operation must not weaken the guarantees of already-migrated flows.

## Requirements *(mandatory)*

### Functional Requirements

**Payment integrity (P1 — F9, F10, F12)**

- **FR-001**: The system MUST record each payment-provider business event's effects (ledger entries, payment-status change, cumulative amounts, receipt) as a single all-or-nothing unit; partially applied effects MUST be impossible to persist or observe.
- **FR-002**: Re-processing any payment-provider event (retry, redelivery, reconciliation) MUST be idempotent: the guard that decides whether work is needed and the work itself MUST commit together.
- **FR-003**: All flows that record a settled payment (one-time checkout, recurring draft, provider-event settlement) MUST use one shared recording path so atomicity and idempotency guarantees cannot diverge per flow.
- **FR-004**: Balance recomputation MUST participate in the same per-property serialization as ledger appends so concurrent writes cannot produce inconsistent running balances.
- **FR-005**: The system MUST provide a means to detect and report historical payment records whose status disagrees with their ledger effects, without silently altering them. Detection runs once at cutover and then recurringly on the existing reconciliation cadence, surfacing findings through logs/alerts (report-only; no automated correction).

**Uniform error behavior (P2 — F2, F5)**

- **FR-006**: Business errors MUST be translated to API responses (code, message, status) by exactly one central mechanism; endpoints MUST NOT contain per-endpoint business-error translation.
- **FR-007**: The shared business-error type MUST live in the domain layer, importable by all features without depending on any other feature.
- **FR-008**: Authenticated-identity attributes (e.g., the caller's property) MUST be resolved through one shared accessor that yields a clean authorization failure when absent; inline parsing of identity claims in endpoints MUST be eliminated.

**Production safety (P3 — F6)**

- **FR-009**: Test-support capabilities (data seeding, test-cleanup endpoints) MUST be disabled in the production environment by an environment-level backstop that configuration flags cannot override; attempted use MUST be logged as a security-relevant event.
- **FR-010**: Test/sample data files MUST be excluded from production build artifacts.

**Contract integrity (P4 — F4, F13, F16)**

- **FR-011**: Client contract types (request/response shapes) MUST be generated from the server's published API description, with regeneration runnable on demand and drift detected by the delivery pipeline (a mismatch fails verification). Generation covers types only: hand-written client services and their mapping code remain and consume the generated types.
- **FR-012**: Each API contract concept MUST have exactly one client-side type definition; identified dead/duplicate definitions MUST be removed and story/test fixtures MUST reference the canonical types.

**Layering and single-definition policies (P5 — F2, F3, F8, F11, F12, F15)**

- **FR-013**: Infrastructure modules MUST NOT depend on feature modules; shared configuration/option types MUST live in a location both can reference.
- **FR-014**: Feature slices MUST NOT import other features' internals for shared concepts; shared kernel concepts MUST live in the domain/shared layer.
- **FR-015**: Monetary policies (major↔minor unit conversion, rounding mode, currency designation) and the scheduled-job authentication check MUST each have a single shared definition.
- **FR-016**: Dead artifacts MUST be removed: the orphaned root container build file and the vestigial UI-scaffold directory; exactly one authoritative container build definition remains.
- **FR-021**: Payment-provider SDK types MUST be confined to the provider gateway adapter; inbound provider events MUST be exposed to feature logic through a gateway-neutral representation so event handlers are testable without constructing provider-SDK objects (F11).

**Developer feedback and delivery structure (P6 — F7, F13, F14, F17–F20)**

- **FR-017**: The test suite MUST be organized into tiers where the unit tier runs with no container or external-service dependency; shared test data MUST be constructible per domain (factories) rather than through one global seed that tests couple to by magic identifiers.
- **FR-018**: Pull-request verification and release/deployment MUST be separate pipelines; repeated pipeline setup MUST be factored into shared reusable steps.
- **FR-019**: The standard and ephemeral environment infrastructure definitions MUST share a common core module for the container service, secrets wiring, and database-branch provisioning.
- **FR-020**: The web client MUST gain shared layers for cross-cutting presentation concerns: a central error-presentation mechanism, a shared API-access convention (base address and error envelope handled once), and decomposition of the two largest payment screens so presentation, wizard/form state, payment-element handling, and API orchestration are separately testable.

### Key Entities

- **Ledger Entry**: An immutable financial event on a property's account (payment, reversal, fee, refund, adjustment) carrying a running balance; the authoritative record of what a homeowner owes and has paid.
- **Payment Transaction**: The lifecycle record of one payment attempt (status, amounts, cumulative refunds, provider references); must always agree with its ledger effects.
- **Provider Event (Webhook Inbox Record)**: A durably stored notification from the payment provider with processing state; the unit of idempotent re-processing.
- **Business Error**: A named, user-presentable failure condition (code, message, status) owned by the domain layer and shared by all features.
- **API Contract Type**: A generated client-side representation of a server request/response shape; exactly one definition per concept.
- **Environment Identity**: The deployment environment designation (production vs. non-production) that gates test-support machinery with precedence over configuration flags.

### Constitution Requirements

- **Tenant boundary**: No change to tenancy semantics. Payment-integrity work (FR-001–FR-005) operates strictly within a single property's account; per-property serialization reinforces the boundary. The shared identity accessor (FR-008) centralizes — not weakens — property-scope checks; cross-property access remains denied by default.
- **Authorization**: All checks remain server-side. FR-008 replaces 24 inline claim parses with one accessor returning a clean authorization failure on missing/invalid identity. FR-009 adds an environment backstop above configuration for test-support surfaces; the cleanup endpoint remains hidden (not-found behavior) when disabled.
- **Ownership and moderation**: Not applicable — no user-generated-content changes.
- **API contract**: The error envelope becomes uniform — `{ code, message }` with the business error's status — for all business errors (FR-006). Success payload shapes, pagination defaults/limits, UTC timestamps, and ID formats are unchanged; client types regenerate from the published description (FR-011), so no breaking changes are introduced.
- **API implementation and docs**: Endpoints remain on the existing FastEndpoints framework. The published OpenAPI description becomes the contract source of truth and must remain accurate; interactive API docs stay development/dev-only and disabled in production (existing behavior preserved and now covered by the P3 backstop tests).
- **Database/runtime**: Payment-integrity changes use the existing strict, idempotent startup-migration discipline (Cloud Run). All-or-nothing recording (FR-001) must respect Neon's low connection limits: transactions stay short-lived, and per-property serialization must not hold connections or locks across external calls to the payment provider.
- **File storage**: No change to the object-storage architecture (Cloudflare R2 in deployed environments, MinIO locally/tests). FR-010 removes sample files from production artifacts; the existing embedded fallback continues to serve local/test needs.
- **Security and abuse controls**: FR-009 logs refused test-machinery invocations as security-relevant events. Existing rate limits are unchanged. Scheduled-job authentication consolidates to one constant-time check (FR-015). No new untrusted-input surfaces are added.
- **Observability**: Centralized error mapping must preserve Sentry error tracking (business errors logged with their code; unexpected errors still reported with trace context and environment/release tags). The shared payment-recording path emits equal-or-better trace/span coverage than the flows it replaces; no sensitive payment data in logs.
- **Accessibility**: The central error-presentation mechanism (FR-020) must render errors as accessible, labeled messages; payment-screen decomposition must preserve keyboard access and WCAG 2.1 AA behavior of the existing flows.
- **Quality gates**: Payment-recording, error-mapping, identity-accessor, and environment-backstop code are hotspot-class and carry the 95% coverage expectation with Sonar static analysis. Idempotency/atomicity scenarios (interrupt-and-retry) get dedicated xUnit tests on Testcontainers, with Theory variations per provider-event type. Tests remain parallel-safe: per-domain factories (FR-017) replace coupling to global seed identifiers. Serilog logging expectations apply to the new security-relevant events. Repowise documentation refresh accompanies each PR. Delivery is staged as vertical slices per user story (P1 first); the cross-cutting moves (FR-013/FR-014) are justified as shared-kernel extraction.
- **Frontend testing**: Regenerated contract types must compile against existing Jasmine/Karma unit tests and component tests; decomposed payment screens keep existing Cypress E2E and Playwright smoke coverage green and gain component tests for the extracted wizard/form/payment-element pieces; Storybook stories reference canonical generated types (FR-012) so visual checks cannot drift from the contract.
- **Executable & living spec**: Every mandatory acceptance scenario maps to an automated, on-demand test: interrupt-and-retry harness (P1), API error-contract tests (P2), production-mode startup tests (P3), contract-drift pipeline check (P4), dependency-rule checks (P5), and tier/pipeline checks (P6). This `spec.md` and its `tasks.md` stay in sync with the code as remediation lands. Where spec 006 (payments) describes flows whose structure changes here, external behavior is preserved, so no contradiction is introduced; if a conflict emerges during implementation, the affected older `spec.md` is reconciled before merge so the spec corpus stays internally consistent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Under fault injection at every intermediate point of provider-event processing followed by retry, 100% of runs end with exactly one ledger entry per business event and a payment status consistent with the ledger (today: reproducible double-post scenarios exist).
- **SC-002**: 100% of documented business-error conditions return the uniform error envelope through the central mapping; hand-written per-endpoint business-error translations drop from 12 to 0 and inline identity-claim parses from 24 to 0.
- **SC-003**: With production environment identity, test-support machinery is inert in 100% of startup configurations — including those with every enabling flag set — and production artifacts contain 0 test/sample data files.
- **SC-004**: 100% of client contract types are generated from the server's API description; a deliberately introduced contract mismatch is caught by verification before release; duplicate or dead contract-type definitions in the client drop to 0 (today: 2 dead types plus 3 parallel payment-shape copies).
- **SC-005**: Automated dependency checks report 0 infrastructure-to-feature references (today: 8 files), 0 cross-feature imports for shared kernel types (today: 11+ files), and 0 provider-SDK type references outside the gateway adapter (today: 3 files), and stay at 0 thereafter.
- **SC-006**: The unit-test tier completes on a machine with no container tooling in under 60 seconds; pull-request verification and release are independently runnable pipelines; the duplicated environment-infrastructure definitions (~350 divergent lines across two modules today) are replaced by one shared core.
- **SC-007**: No behavior regression: the full existing automated test suite (backend and frontend) passes after every remediation stage, and payment end-to-end flows remain green throughout.

## Assumptions

- The findings report `docs/architecture-smells-analysis.md` (commit `19ac12a`) is the agreed factual basis; no re-audit is in scope.
- Scope is the full remediation program (P1–P6), staged by the priorities above and committed in full (see Clarifications). Each user story is independently deliverable and P1 alone is a viable first release; descoping P5/P6 requires an explicit decision that amends this spec.
- Behavior preservation is the default: apart from the deliberate changes named here (uniform error envelope, environment backstop for test machinery), user-visible behavior does not change — this is structural remediation, not feature work.
- The anemic-domain finding is accepted as a deliberate style for now: business invariants remain in services. This spec centralizes cross-cutting *policies* (money, errors, identity) without mandating a rich-domain rewrite.
- Historical data reconciliation (FR-005) is detection-and-report only; correcting past double-posts (if found) is a separate, human-approved operation.
- The payment provider's event delivery remains at-least-once, with duplicates and out-of-order delivery possible; the design must not assume exactly-once delivery from the provider.
- Existing specs 001–014 remain authoritative for their features' behavior; this spec governs structure and cross-cutting guarantees only.

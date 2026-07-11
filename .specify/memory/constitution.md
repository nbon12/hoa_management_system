<!--
Sync Impact Report
==================
Version change: 3.0.0 -> 3.1.0
Modified principles: None (no existing principle redefined or removed).
Added sections:
  New section 12 "Spec Independence & Parallelism" — each spec or sub-spec MUST be
    individually completable (implementable, testable, mergeable) without requiring
    another spec to land first, absent an explicitly documented hard dependency; when a
    larger effort is split across specs, the split SHOULD be designed so the resulting
    specs CAN be worked on in parallel.
  Prior "Governance & Amendments" renumbered from section 12 to section 13.
Templates requiring updates:
  .specify/templates/plan-template.md ✅ updated (Constitution Check: spec independence bullet)
  .specify/templates/spec-template.md ✅ updated (Constitution Requirements: spec independence bullet)
  .specify/templates/tasks-template.md ✅ updated (Dependencies & Execution Order: cross-spec note)
  CLAUDE.md ✅ updated (guidance to not ask about work order; just pick and start)
Version bump rationale (MINOR): new principle section added; no existing rule removed or
  redefined.
Follow-up TODOs: None

----- prior amendment -----
Version change: 2.2.0 -> 3.0.0
Modified principles:
  Technology Stack (section 2) — REMOVED the Auth0 mandate. Authentication/authorization is now
    provider-agnostic. The current, compliant implementation is in-application: ASP.NET Core
    Identity for credential storage/password hashing + JWT bearer access tokens with rotating,
    single-use, hashed refresh tokens. A specific third-party identity-provider vendor is NOT
    mandated (a managed IdP MAY be adopted later via amendment if it meets section 7).
  Security & Authentication (section 7) — replaced Auth0-specific identity/token-validation
    mandates with provider-agnostic strong-authentication + server-side HOA-scoped authorization
    requirements. The security bar is unchanged; only the vendor lock-in is removed.
  Infrastructure & Environments (section 10) — "separate Auth0 configuration" generalized to
    "separate authentication configuration/secrets".
Templates requiring updates:
  .specify/templates/plan-template.md ✅ updated (Auth0 removed from Constitution Check)
  .specify/templates/tasks-template.md ✅ updated (foundational auth task made provider-agnostic)
  .specify/templates/spec-template.md ✅ no Auth0 reference (n/a)
Version bump rationale (MAJOR): removal/redefinition of a non-negotiable technology mandate
  (Auth0) within governed principle sections 2 and 7.
Follow-up TODOs: None

----- prior amendment -----
Version change: 2.1.0 -> 2.2.0
Modified principles:
  Added new section "Executable & Living Specifications" (now section 11) enforcing that
    every spec.md remains executable at all times (acceptance criteria backed by runnable,
    currently-passing tests), that spec.md is a living document kept in sync with reality
    (drift is a defect to be fixed before merge — including for older, already-merged specs),
    that the active feature's spec.md and tasks.md must be current before a PR is submitted
    (older specs only need their spec.md kept truthful; tasks.md/plan.md/research.md are
    point-in-time artifacts not required to be refreshed), and that cross-spec contradictions
    (a new/amended spec whose tests contradict a former spec) MUST be reconciled so the full
    spec corpus stays internally consistent.
  Renumbered prior "Governance & Amendments" from section 11 to section 12.
Templates requiring updates:
  .specify/templates/tasks-template.md updated (pre-PR spec/tasks freshness + drift task,
    Polish review item)
  .specify/templates/plan-template.md updated (Constitution Check: executable/living specs)
  .specify/templates/spec-template.md updated (Constitution Requirements: executable spec +
    cross-spec consistency)
Follow-up TODOs: None

----- prior amendment -----
Version change: 2.0.0 -> 2.1.0
Modified principles:
  Operations, Secrets, and Data Lifecycle — added mandatory startup configuration
    validation: all backend options via FluentValidation (fail-fast, environment-aware,
    incl. ASPNETCORE_ENVIRONMENT), and a frontend boot-time guard that fails loudly on
    missing required configuration. Codifies the 008-config-validation feature.
Templates requiring updates: none (additive rule).
Follow-up TODOs: None

----- prior amendment -----
Version change: 1.1 -> 2.0.0
Modified principles:
  Project Purpose — expanded HOA scope, multi-association membership, and content ownership.
  Technology Stack — replaced Okta with Auth0, added Angular hosting, FastEndpoints,
    Neon, Cloud Run, Cloudflare, Docker Hub, Sentry, Swashbuckle, Repowise, R2/MinIO,
    Sonar, and Codecov requirements.
  Backend Principles — added FastEndpoints, Swagger/OpenAPI, production-safe errors,
    and pagination standards.
  Frontend Principles — expanded accessibility, API contract, and testing requirements.
  Security & Authentication — replaced Okta-specific rules with Auth0 and HOA-scoped
    server-side authorization.
  Quality & Code Standards — expanded backend, frontend, CI/CD, coverage, and
    documentation gates.
  Spec Kit Testing Constitution — expanded PostgreSQL/Testcontainers, test-first,
    theories, and completion gate guidance.
Added sections:
  Tenancy, Data, and Product Invariants; API Contract Standards; Operations, Secrets,
  and Data Lifecycle; Infrastructure & Environments.
Removed sections: None.
Templates requiring updates:
  .specify/templates/plan-template.md updated (constitution gate details)
  .specify/templates/spec-template.md updated (quality gates prompt)
  .specify/templates/tasks-template.md updated (test-first and Repowise tasks)
Follow-up TODOs: None
-->

# HOA Management Company Constitution

**Version**: 3.1.0 | **Ratified**: 2026-03-14 | **Last Amended**: 2026-07-04
**Authors**: Project maintainers

## 1. Project Purpose

This project provides **HOA Management Company**, a web application that lets homeowners,
board members, HOA managers, and platform operators manage HOA communities, properties,
violations, assessments, documents, announcements, and related workflows. All features MUST
align with:

- Security, privacy, and strong authentication for all user types.
- Consistency and predictability of API behavior.
- Maintainability and scalability of code.
- Accessibility and responsiveness across devices.
- **Multi-association membership**: a single user MAY belong to **multiple** HOAs or
  associations; the data model and APIs MUST support this without ambiguity.
- User-generated HOA content, documents, notes, evidence, and announcements MUST have clear
  ownership, association scope, visibility, and moderation/audit rules where applicable.

## 2. Technology Stack

All implementations MUST conform to the following:

- **Frontend**: Angular, hosted on **Cloudflare Pages**.
- **Backend**: **.NET** REST API using **FastEndpoints** for all application endpoints
  (HTTP verbs: GET, POST, PUT, PATCH, DELETE as appropriate).
- **Database**: **PostgreSQL**, single logical database with **shared tables** (no per-HOA
  database silos unless explicitly amended).
- **Database hosting**: **Neon** PostgreSQL with **scale-to-zero** in non-production and
  production as configured; separate Neon databases per environment (see section 10).
- **Authentication / authorization**: Implemented **in-application** using **ASP.NET Core
  Identity** (credential storage and password hashing) and **JWT bearer tokens** for API access,
  with **rotating, single-use, hashed refresh tokens**. A specific third-party identity provider
  is **NOT** mandated; a managed IdP MAY be adopted later via a constitution amendment provided it
  meets the Security & Authentication requirements in section 7.
- **Containers**: **All backend services MUST be Dockerized**. Images MUST be published to
  **Docker Hub** (organization/repo naming per project standards).
- **Backend runtime**: Containers deployed to **Google Cloud Run**, configured to **scale to
  zero**; **cold start latency is acceptable**.
- **Edge / perimeter**: **Cloudflare** MUST sit in front of public traffic for **DDoS
  protection**, **rate limiting**, **bot filtering**, **traffic filtering**, and **edge caching**
  where appropriate - including **in front of the backend** API surface exposed to clients.
- **CI/CD**: **GitHub Actions**; deployments MUST run automatically on merges to **`main`**
  (with environment promotion rules as defined in pipeline configuration).
- **Testing**: Unit tests, integration tests, and UI tests per the Spec Kit Testing
  Constitution below.
- **Observability**: **Sentry** MUST be used for error tracking, performance visibility, and
  trace context across frontend and backend services.
- **API documentation**: **Swashbuckle** MUST generate OpenAPI and Swagger UI for .NET APIs
  in development only.
- **Repository intelligence docs**: **Repowise** indexes the codebase and emits documentation
  in repository-defined marker regions (per project Repowise/MCP configuration). Pull requests
  MUST include regenerated or updated Repowise outputs in those regions so indexed
  documentation, ownership, architectural decisions, and related signals stay accurate.
- **File storage**: If file/blob storage is added, hosted environments MUST use
  **Cloudflare R2**. Local Docker Compose and local/CI tests MUST use **MinIO** to simulate
  object storage behavior.

## 3. Tenancy, Data, and Product Invariants

- Every HOA-scoped row MUST include an `hoa_id`, `association_id`, or equivalent tenant
  boundary.
- Shared tables MUST avoid accidental global queries. Queries that intentionally cross HOAs
  MUST document the reason, authorization model, and expected result scope.
- Cross-HOA access MUST be denied by default.
- A user MAY belong to multiple HOAs or associations.
- An HOA MUST have at least one owner/admin or management administrator at all times.
- User-generated HOA content MUST identify its owner, HOA scope, visibility, and
  moderation/audit state where applicable.
- Schema changes MUST be represented as migrations.
- Migrations MUST be tested against PostgreSQL.
- Database changes MUST use strict migrations; manual database edits are not allowed.
- Cloud Run startup MUST apply migrations idempotently and safely.
- Destructive migrations require an explicit rollback or mitigation plan.
- Seed and reference data MUST be handled predictably per environment.
- IDs SHOULD use a consistent format across APIs and storage; UUIDs are preferred unless
  a feature plan documents a better fit.
- Dates and times MUST be stored and exposed in UTC unless a field explicitly represents
  a user-entered local date/time concept.

## 4. Backend Principles

### Statelessness

- APIs MUST remain **stateless**; no reliance on in-process session state for correctness.
- **All persistent state MUST be stored in PostgreSQL** (and external durable services
  explicitly approved in plan/spec if added later).
- Function and handler design MUST minimize unnecessary side effects; prefer pure functions
  where practical.

### Endpoint implementation

- Application API endpoints MUST be implemented with **FastEndpoints**.
- MVC controllers MUST NOT be introduced for application endpoints unless a feature plan
  documents a specific compatibility reason and an amendment approves it.

### Swagger and OpenAPI

- **Swashbuckle** MUST generate OpenAPI documentation and Swagger UI for the .NET API in
  development environments.
- Swagger UI MUST be available at `/swagger` in development to support debugging, auth-flow
  trials, bug reproduction, and shareable diagnostic links.
- Swagger and its OpenAPI endpoint MUST be disabled entirely in production.
- Swagger documentation MUST be generated from the implemented API endpoints and kept aligned
  with API contracts.

### Functional paradigm

- Prefer **functional design** where appropriate (immutable DTOs, explicit inputs/outputs,
  composition).
- Use factories to create test data and setup relationships; factories MUST NOT modify objects
  they did not create.
- Application services MAY coordinate side effects and transactional boundaries.

### Error handling

- A **global exception handler** MUST catch unhandled errors and map them to HTTP responses.
- APIs MUST return **consistent, meaningful error response shapes** (stable codes/types for
  clients).
- In **developer** environments, responses MAY include detailed error messages for debugging.
- In **production**, responses MUST **NOT** leak stack traces, internal paths, or other
  **system information**; use generic client-safe messages and log details server-side only.

### Pagination

- Every endpoint that returns a **collection** MUST support pagination via **`limit`** and
  **`offset`** query parameters (unless superseded by a documented cursor standard in a
  future amendment; until then, limit/offset is mandatory).
- Collection endpoints MUST document the default `limit` and maximum allowed `limit`.

## 5. API Contract Standards

- API responses MUST use a consistent envelope or a documented response shape.
- Error responses MUST use the same documented shape across endpoints.
- Breaking API changes require contract updates and migration notes.
- Contracts MUST document collection pagination, default `limit`, maximum `limit`,
  authentication requirements, authorization rules, and cacheability.

## 6. Frontend Principles

### Responsiveness

The UI MUST function and render correctly at:

- Phone widths (e.g., iPhone-class)
- Tablet widths
- Desktop widths

### Consistency

- UI components MUST follow a consistent design system and Angular conventions.
- User flows MUST be validated against backend **API contracts**; breaking contract changes
  require coordinated releases or versioning.
- Static assets SHOULD be cacheable with hashed filenames.
- UI SHOULD target WCAG 2.1 AA where practical.
- Forms, navigation, dialogs, property/violation workflows, and HOA content creation flows
  MUST be keyboard-accessible.
- Meaningful labels and validation messages are required.

## 7. Security & Authentication

- Identity and token issuance/validation are handled by the application's own authentication
  system (currently ASP.NET Core Identity for credentials and JWT bearer tokens for API access);
  no specific third-party identity-provider vendor is mandated.
- All protected endpoints and UI actions MUST enforce authentication; access to protected
  resources by unauthenticated callers MUST be denied.
- Access tokens MUST be validated against a pinned signing algorithm; refresh tokens MUST be
  rotating, single-use, and persisted only as hashes (never in plaintext).
- Application authorization is enforced server-side based on the authenticated user,
  HOA membership, and role in the target HOA.
- Authorization MUST always check both the authenticated user and their membership/role
  in the target HOA.
- Authorization MUST distinguish roles appropriate to the product (e.g., HOA creator,
  owner/admin, board member, property manager, homeowner/resident, committee member,
  platform operator) and MUST enforce least privilege.
- Frontend authorization checks are UX-only and MUST NOT be trusted for enforcement.
- Backend authorization policies MUST be covered by integration tests.
- Sensitive data MUST be encrypted **in transit** (TLS end-to-end) and **at rest** per
  provider capabilities (Neon, Cloud Run, secrets management).
- APIs MUST NOT return more data than necessary for the authenticated principal and context.
- User-generated content MUST be treated as untrusted input.
- Security-sensitive events MUST be logged, including login-linked identity changes,
  membership changes, role changes, property ownership/residency changes, deletes, and
  publish/unpublish actions.
- Moderation, manager, and board/admin actions SHOULD be auditable.
- Rate limiting MUST apply to auth-adjacent, content creation, invite, violation submission,
  document upload, and public discovery endpoints.

## 8. Operations, Secrets, and Data Lifecycle

- Secrets MUST NOT be committed.
- Environment-specific configuration MUST come from environment variables or managed secret
  stores.
- **All application configuration MUST be validated at startup.** On the backend, every
  strongly-typed options/configuration class MUST be validated with **FluentValidation**
  (bound-and-validated at startup); the service MUST **fail fast** — refuse to start — on invalid
  or missing configuration rather than deferring failures to request time. Validation rules MUST be
  **environment-aware**, and MUST include validating the runtime environment name
  (`ASPNETCORE_ENVIRONMENT`) against the known set (`Development` = local machine, `Dev` = deployed
  dev, `Test`, `Staging`, `Production`) so a mis-set environment (such as `prod` instead of
  `Production`, or the deployed-`Dev`-vs-local-`Development` confusion) is rejected at boot.
  The **frontend** MUST apply the equivalent boot-time guard for its required configuration, failing
  loudly (refusing to boot / surfacing a clear error) when required values are missing or invalid.
  New configuration of any kind MUST ship with its validator.
- Dev, Staging, and Prod secrets MUST be isolated.
- Docker images MUST NOT bake in secrets.
- Backend services MUST emit structured JSON logs.
- Backend services MUST use **Serilog** for structured logging.
- Request correlation IDs SHOULD be propagated through API logs.
- Sentry MUST be configured in both the Angular frontend and .NET backend for error tracking
  and performance visibility.
- Sentry trace context MUST propagate across frontend requests and backend handling so a
  user-facing issue can be followed through the full request path.
- Sentry events MUST include environment and release identifiers and MUST NOT capture secrets
  or sensitive HOA, homeowner, resident, property, violation, payment, or document content.
- Health/readiness endpoints SHOULD exist for Cloud Run services.
- Production errors MUST be logged with enough detail to debug without leaking details to
  clients.
- Because Neon is the hosted PostgreSQL provider, database access MUST use low maximum
  connection counts, pooling enabled, and short-lived DbContext instances.
- File/blob storage, if introduced, MUST store binary objects in Cloudflare R2 for hosted
  environments and MUST use MinIO in local Docker Compose/test environments.
- PostgreSQL MUST store file metadata, ownership, HOA scope, and references, not large
  binary file payloads.
- API responses MUST only be cached when explicitly safe.
- Authenticated or user-specific responses MUST NOT be edge-cached unless carefully keyed
  and justified in the feature plan.

## 9. Quality & Code Standards

### Backend

- Adhere to .NET naming conventions and team code style.
- Use CQRS where it fits the application boundary and existing project patterns.
- Backend services MUST use **Serilog** for structured logging.
- New logic MUST have **unit tests**; persistence and integrations MUST have **integration
  tests** (PostgreSQL, see testing constitution).
- **Docker Compose** for local development MUST approximate production: **a running
  PostgreSQL service MUST be part of Compose** so local and CI test runs use realistic DB
  semantics.

### Frontend

- Follow Angular best practices and linting; component and service unit tests are required
  for non-trivial behavior.
- Angular unit tests MUST use **Jasmine** and **Karma**.
- Angular component tests MUST use **Angular Testing Library**.
- Frontend browser tests MUST use **Playwright** where browser automation is required outside
  the end-to-end suite.
- End-to-end tests MUST use **Cypress**.
- **Storybook** MUST be used for component stories and visual regression testing.

### CI/CD

- Automated checks MUST enforce linting and tests before merge where configured.
- Pull requests MUST pass static code analysis via **Sonar** before merge.
- A **GitHub Actions** workflow MUST kick off the Sonar scan for pull requests.
- Pull requests MUST publish Codecov results through **GitHub Actions**.
- Pull requests MUST include updates to **Repowise**-generated content (run the project's
  Repowise workflow and commit refreshed outputs in Repowise-maintained regions; no-op if
  nothing changed) so repository intelligence documentation does not drift from merged code.
- Code coverage MUST be at least **95%** for all relevant files changed or added by a pull
  request.
- Pipelines MUST deploy **Dev / Staging / Prod** with **isolated** Neon databases and
  **isolated** Cloud Run services (or equivalent environment separation).

## 10. Infrastructure & Environments

- **Dev**, **Staging**, and **Prod** MUST use **separate** PostgreSQL databases and
  **separate** Cloud Run services (and separate authentication configuration/secrets as applicable).
- **Cloudflare** configuration MUST align per environment (Pages for frontend; edge rules for
  API protection and caching in front of backend endpoints).
- **Local development**: `docker-compose` (or successor) MUST bring up dependencies so that
  developers and automated tests can run against a **real PostgreSQL** instance analogous to
  production semantics.

## 11. Executable & Living Specifications

Specifications are not documentation that lags behind the code — they are the executable
contract of the system and MUST stay true at all times.

- **Always executable**: Every feature spec (`spec.md`) MUST remain executable at all times.
  Each mandatory acceptance scenario and functional requirement MUST be backed by at least
  one automated test (backend integration/business-process test or frontend
  unit/component/E2E test) that can be run on demand and that **currently passes** against
  the merged code. A spec whose acceptance criteria cannot be executed, or whose tests fail,
  is a defect that MUST be fixed before merge.
- **No unverified claims**: A spec MUST NOT describe behavior that no executable test
  verifies. Acceptance scenarios MUST be traceable to tests, and tests MUST be traceable
  back to acceptance criteria.
- **Living and truthful (`spec.md` only)**: `spec.md` is a living document and MUST reflect
  the system as it is actually built. When implemented behavior diverges from a `spec.md` —
  **including older, already-merged specs** — the divergence MUST be resolved before the change
  merges: either the `spec.md` is updated so it reflects reality, or the code is corrected to
  match the spec. **Spec drift is a defect**, not an acceptable steady state. Only `spec.md`
  carries this freshness obligation; `tasks.md`, `plan.md`, and `research.md` are point-in-time
  artifacts and are **NOT** required to be kept up to date for prior, already-merged features.
- **Pre-PR freshness**: Before a pull request is submitted, the **active feature's** `spec.md`
  **and** `tasks.md` MUST be brought up to date with the work actually performed, and **any
  older `spec.md` that has drifted from the code MUST be updated** so it reflects reality. The
  active feature's `plan.md` and `research.md` are NOT required to be refreshed for this gate,
  and the `tasks.md`, `plan.md`, and `research.md` of older, already-merged features are
  likewise NOT required to be refreshed — only their `spec.md` must stay truthful.
- **Cross-spec consistency**: When a new or amended spec introduces tests or acceptance
  criteria that **directly contradict** those of a former spec, the contradiction MUST be
  reconciled before merge. Reconciliation means: update the superseded spec(s) so the full
  spec corpus is internally consistent, explicitly record which spec prevails and why, and
  ensure no two specs assert contradictory assertions that would both be expected to pass.
- **Corpus invariant**: The full body of specs MUST be free of mutually contradictory
  executable assertions at all times.

## 12. Spec Independence & Parallelism

- Each spec or sub-spec MUST be **individually completable**: implementable, testable, and
  mergeable on its own, without requiring another spec to land first — unless an explicit,
  documented hard dependency exists (e.g., a schema or contract this spec's tests require
  from another spec).
- When a larger effort is split across multiple specs or sub-specs, the split SHOULD be
  designed so the resulting specs **CAN be worked on in parallel** by different
  contributors or agents without blocking one another.
- Hard dependencies between specs (spec B cannot start or merge until spec A merges) MUST
  be the exception, MUST be explicitly documented in both specs' `plan.md`, and MUST be
  minimized during spec planning — prefer restructuring the split to remove the dependency
  over accepting it.
- A spec MUST NOT assume a sibling spec has already been implemented unless that
  dependency is documented as above.
- This complements the cross-spec consistency rule in section 11: independence governs
  how specs are split and sequenced; consistency governs what happens when their
  executable assertions overlap or conflict.

## 13. Governance & Amendments

- Changes to this constitution MUST be reviewed and approved like any architectural decision
  record affecting the whole project.
- Pull requests SHOULD be focused on vertical slices that deliver independently testable
  increments. Horizontal pull requests are allowed for cross-cutting infrastructure,
  quality, security, or refactoring work. Broad mixed-scope pull requests MUST be split
  or explicitly justified.
- The project MUST be built incrementally and iteratively; implementation plans MUST NOT
  attempt to deliver unrelated product surface area all at once.
- Each amendment MUST document rationale, author, and version bump per semantic versioning:
  - **MAJOR**: Breaking governance or removal/redefinition of non-negotiable rules.
  - **MINOR**: New principle, section, or materially expanded guidance.
  - **PATCH**: Clarifications, wording, typos, non-semantic refinements.
- Prior versions SHOULD be retained in version control history for auditability.

---

# Spec Kit Testing Constitution

(.NET + PostgreSQL, transaction-per-test isolation)

## Purpose

This constitution defines rules for automated tests that use **xUnit** and **.NET
Testcontainers** for containerized dependencies. Tests are:

- **Deterministic** - repeatable and order-independent
- **Isolated** - test data does not leak across tests
- **Realistic** - PostgreSQL with production-like semantics
- **Scalable** - maintainable as the system and team grow

It applies to unit, repository, business-process, and end-to-end tests.

## 1. Core Definitions

### 1.1 Business process

A **business process** is a named operation that coordinates domain rules, state transitions,
and side effects to achieve an outcome. It typically lives in an application service or use
case, may span repositories, and may emit events or notifications.

**Examples:** `CreateHoa`, `InviteMember`, `RecordViolation`, `PublishAnnouncement`
**Not a business process:** repository CRUD, trivial mapping, or pure domain queries with no
orchestration.

### 1.2 Repository

A **repository** abstracts persistence: CRUD, queries, projections, and transaction boundaries
for stored data. Repositories are not business processes and are tested separately.

## 2. Guiding principles

### 2.1 Universal isolation

All tests that touch the database MUST run inside a PostgreSQL **transaction that rolls back**
after the test (or use an equivalent pattern approved in writing). This ensures isolation,
safe parallelism, and no cross-test contamination.

Tests MUST be written with the assumption that other tests are running in parallel and that
previous test runs may have created data, containers, files, messages, or other artifacts.
Tests MUST use unique data, scoped resources, transactions, cleanup, or idempotent setup so
they do not depend on a clean global environment.

**Example (C# + EF Core):**

```csharp
await using var transaction = await dbContext.Database.BeginTransactionAsync();
// arrange + act + assert
await transaction.RollbackAsync();
```

### 2.2 PostgreSQL usage

Persistence-related tests MUST use **PostgreSQL**. In-memory providers MUST NOT replace
PostgreSQL for integration or repository tests.

Integration tests MUST use **.NET Testcontainers** to spin up Docker containers for
PostgreSQL and other required local dependencies.

### 2.3 Factories

Factories declare **valid data** for tests and MUST NOT embed business rules or conditional
side effects.

**Allowed:** explicit field values, required FKs, defaults.
**Prohibited:** branching domain logic inside factories that changes real system behavior.

### 2.4 Test-first (red-green)

Tests MUST be written **before** implementation where this constitution applies to a task.

- Tests MUST be traceable to specification acceptance criteria before implementation begins.
- Non-compiling tests MAY be temporarily commented with a clear path to restore them.
- Implementation MUST proceed in **red -> green** cycles until tests pass.

### 2.5 Data-varied tests with Theories

Tests that validate the same behavior across multiple input values, roles, pagination
boundaries, authorization states, validation cases, or error cases MUST use xUnit
**Theories** with explicit test data (`InlineData`, `MemberData`, or `ClassData`).

Repeated copy/paste Facts for data variations are prohibited unless a test documents why the
cases have materially different setup, behavior, or assertions.

## 3. Repository test constitution

- Repository tests MUST use per-test transaction isolation.
- Required rows MUST be created in-test or via factories.
- Assertions focus on **structural** correctness: CRUD, constraints, query shapes - not full
  business outcomes.

## 4. Business-process test constitution

- Business-process tests MUST use per-test transaction isolation.
- Each test builds minimal valid domain state (factories), without calling unrelated processes
  unless the scenario is explicitly end-to-end.
- Assertions target **business outcomes**, not repository internals.

## 5. End-to-end test constitution

- Few in number; may span multiple processes and real infrastructure boundaries where
  justified.
- Still require deterministic data setup/teardown policies.
- Focus on **workflow** correctness, not low-level SQL.

## 6. Cross-cutting rules

- **Order independence**, **repeatability**, **single-reason failures**, **data ownership** by
  each test, **parallel safety**, **rerun safety**, and **production faithfulness** for
  PostgreSQL semantics.

## 7. Recommended folder layout

```text
spec_kit/
  testing/
    constitution.md             # Optional modular copy or supplement
    repository.constitution.md
    business_process.constitution.md
    end_to_end.constitution.md
  factories/
    UserFactory.cs
    HoaFactory.cs
  fixtures/
    TestDatabaseFixture.cs
  migrations/
    (EF Core / SQL migrations as owned by the backend)
```

## 8. Strategic outcome

- **Repository tests:** persistence correctness without domain coupling
- **Business-process tests:** domain behavior without unrelated orchestration
- **End-to-end tests:** full workflows, sparingly
- **All tests:** isolated, deterministic, PostgreSQL-faithful, safe for parallel execution

## 9. Completion gate

Implementation for a feature or task MUST NOT be considered complete until:

- **Backend integration tests** run locally (or in CI) and **all pass**, and
- **Frontend unit, component, Playwright, Cypress, and Storybook visual regression tests**
  run locally (or in CI) where applicable and **all pass**.

# Feature Specification: Dev Environment Auto-Deploy on Merge to Main

**Feature Branch**: `009-dev-auto-deploy`  
**Created**: 2026-06-13  
**Status**: Partially delivered (PR #35) — see Delivery Status below  
**Input**: User description: "I want to create a dev environment that automatically gets pushed to upon merges to main. review the constitution for what it should be setup like."

## Delivery Status & Deferred Scope *(PR #35)*

**Shipped in PR #35** (the `deploy-dev` pipeline is **gated off** via `DEV_DEPLOY_ENABLED`, so the
PR is safe to merge before any cloud resources exist):

- Config-driven backend startup (migrations / seed / Swagger / CORS) so a deployed `Dev` service
  behaves correctly (FR-004, FR-004a, FR-011a, Swagger gate) — implemented + tested.
- Frontend `dev` build configuration pointed at the Dev API (FR-005) — build-verified.
- The `deploy-dev` GitHub Actions job: candidate deploy, health + E2E gate, promotion, latest-wins
  concurrency, failure notification (FR-001, FR-003, FR-006–FR-009) — **authored & YAML-validated,
  not yet run against live infrastructure.**

**Deferred (not in PR #35):**

- **Environment provisioning** (the isolated Neon DB, Cloud Run service, Cloudflare Pages/DNS/R2,
  Secret Manager, WIF — FR-010/FR-011/FR-015/FR-016 *infrastructure*) is delivered declaratively by
  a follow-up **Infrastructure-as-Code feature** (OpenTofu) rather than by hand — see
  [`HANDOFF-infra-as-code.md`](./HANDOFF-infra-as-code.md).
- **Live verification** of the isolation and success criteria (SC-002, SC-005, SC-006, SC-009, and
  the US3/US4 acceptance runs) happens after the first real deploy once provisioning and
  `DEV_DEPLOY_ENABLED=true` are in place.
- **Known limitation:** the E2E gate runs the Cypress suite, which currently stubs the backend
  in-spec — so it validates the Dev frontend but not yet the Dev backend end-to-end. Promoting it to
  a true integration gate is a follow-up.

## User Scenarios & Testing *(mandatory)*

This feature establishes a **Dev** environment and a fully automated continuous-deployment
path so that whatever is merged to `main` is automatically built, validated, and released to
Dev with no manual steps. The "users" are the engineering team, QA/reviewers, and stakeholders
who need a continuously up-to-date, isolated environment to validate merged work.

### User Story 1 - Backend auto-deploys to Dev on merge to main (Priority: P1)

When a developer merges a pull request to `main`, the backend API is automatically built into a
container image, published to the image registry, and released to the Dev backend service,
with database migrations applied automatically — without anyone running a manual deploy step.

**Why this priority**: This is the core of the feature and the minimum viable product. Without
automated backend delivery, there is no Dev environment to validate merged work against. It
delivers standalone value the moment it works: every merge becomes a live, testable Dev API.

**Independent Test**: Merge a trivial backend change to `main` and confirm that, with no manual
intervention, the Dev backend serves the new behavior and any new migration has been applied to
the isolated Dev database.

**Acceptance Scenarios**:

1. **Given** a pull request that passes all required checks, **When** it is merged to `main`,
   **Then** an automated pipeline builds the backend container image, publishes it to the
   registry with a traceable version/tag, and releases it to the Dev backend service.
2. **Given** the merged change includes a new schema migration, **When** the Dev backend service
   starts the new release, **Then** the migration is applied automatically and idempotently
   against the isolated Dev database before the service begins serving traffic.
3. **Given** a successful Dev release, **When** a reviewer calls the Dev API, **Then** the
   response reflects the newly merged backend behavior.
4. **Given** two merges to `main` in quick succession, **When** the pipeline runs, **Then** the
   later commit ends up as the live Dev version (no stale earlier commit wins).

---

### User Story 2 - Frontend auto-deploys to Dev on merge to main (Priority: P2)

When a developer merges a pull request to `main`, the Angular frontend is automatically built
and published to the Dev frontend hosting, pointed at the Dev backend API, so the full Dev
application reflects the merged change end to end.

**Why this priority**: A Dev API alone is not enough for stakeholders and QA to validate user
journeys. Pairing automated frontend delivery with the backend gives a complete, continuously
updated Dev application. It is P2 because the backend path (P1) is the foundational deliverable
and can ship and be validated first.

**Independent Test**: Merge a visible frontend change to `main` and confirm the Dev frontend URL
serves the updated UI, talking to the Dev backend, with no manual deploy step.

**Acceptance Scenarios**:

1. **Given** a merged frontend change, **When** the pipeline runs, **Then** the Dev frontend is
   rebuilt and published to Dev hosting automatically.
2. **Given** the Dev frontend is published, **When** a user loads the Dev frontend URL, **Then**
   it communicates with the Dev backend (not Staging or Prod).
3. **Given** a frontend build fails, **When** the pipeline runs, **Then** the previously published
   Dev frontend remains available and unchanged.

---

### User Story 3 - Failed deploys never take down the running Dev environment (Priority: P3)

A deployment that fails to build, fails migrations, or fails its health/readiness check must not
replace the currently running, healthy Dev release. The Dev environment stays available on the
last good version, and the failure is clearly visible to the team.

**Why this priority**: Continuous deployment is only trustworthy if a bad merge degrades nothing
that was already working. This protects the team's ability to keep using Dev. It is P3 because
it hardens the P1/P2 happy paths rather than introducing new delivery capability.

**Independent Test**: Intentionally merge a change that fails its health check (or a broken
migration) and confirm the previously running Dev release continues to serve traffic and the
pipeline reports a clear failure.

**Acceptance Scenarios**:

1. **Given** a new release that fails its health/readiness check, **When** the pipeline attempts
   to promote it, **Then** the new release does not receive traffic and the prior healthy release
   continues serving Dev.
2. **Given** a migration that fails to apply, **When** the new backend release starts, **Then**
   the release is not marked healthy and is not promoted to serve Dev traffic.
3. **Given** any deploy failure, **When** the pipeline finishes, **Then** the failure status and
   the offending commit are reported to the team through the pipeline's status surface.

---

### User Story 4 - Dev environment is isolated and self-configuring (Priority: P3)

The Dev environment uses its own isolated database, identity configuration, and secrets — fully
separated from Staging and Prod — sourced from a managed secret store at deploy/run time and
never committed to the repository or baked into images.

**Why this priority**: Isolation prevents Dev activity from touching Staging/Prod data and keeps
the constitution's environment-separation and secrets-handling invariants intact. It is P3
because it constrains *how* P1/P2 deploy rather than adding a new user-visible capability, but it
is mandatory for the feature to be acceptable.

**Independent Test**: Inspect the deployed Dev backend and confirm it connects to the Dev-only
database and identity configuration, that no secret values appear in the repository or image
layers, and that the same secret names resolve to different values than Staging/Prod.

**Acceptance Scenarios**:

1. **Given** the Dev backend is running, **When** it reads configuration, **Then** all
   environment-specific values (database connection, identity config, third-party keys) come from
   environment variables or a managed secret store, not from committed files or image contents.
2. **Given** the Dev and Staging/Prod environments, **When** their configuration is compared,
   **Then** they reference separate databases and separate identity/service configurations.
3. **Given** the deployed container image, **When** its layers are inspected, **Then** no secret
   values are present in the image.

---

### Edge Cases

- **Concurrent merges**: Two merges land before the first finishes deploying — the pipeline must
  converge on the latest commit as the live Dev version rather than leaving an older commit live.
- **Migration that is slow or long-running**: Startup migration takes longer than the platform's
  startup timeout — the readiness signal must account for migration time so a valid release is
  not wrongly judged unhealthy.
- **Destructive migration**: A migration that drops or rewrites data must have a documented
  rollback or mitigation path before it can reach Dev.
- **Registry or hosting outage**: The image registry or frontend hosting is temporarily
  unavailable — the pipeline fails clearly and the prior Dev release is unaffected.
- **First-ever deploy (cold environment)**: The Dev environment has never been deployed — the
  pipeline provisions/initializes it (including an empty Dev database getting its baseline
  migrations) without manual steps.
- **Secret missing or rotated**: A required Dev secret is missing or was rotated — the deploy
  fails safely with a clear message rather than starting a misconfigured service.
- **Reverted merge**: A revert is merged to `main` — Dev redeploys the reverted state the same
  way as any other merge.

## Clarifications

### Session 2026-06-13

- Q: How should access to the Dev environment be restricted? → A: Auth0-gated only — reachable by anyone with a Dev login behind the Cloudflare edge; no additional IP/VPN network restriction.
- Q: What data should the Dev database contain after each deploy? → A: Apply baseline migrations, then idempotently seed reference/synthetic data (reusing the existing TestDataSeeder pattern).
- Q: How deeply should a deploy be verified before it's declared successful? → A: Run the full E2E (Cypress/Playwright) suite against Dev; the deploy is promoted/declared successful only if it passes.
- Q: Where should deploy status be reported? → A: GitHub Actions run + commit/deployment status for every deploy, plus a push to team chat on failure only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST automatically trigger a Dev deployment pipeline on every merge to
  the `main` branch, with no manual step required to start it.
- **FR-002**: The pipeline MUST build the backend into a container image and publish it to the
  designated image registry, tagged with a traceable identifier (e.g., commit SHA and/or release
  version) for each merge.
- **FR-003**: The pipeline MUST release the newly built backend image to the **Dev backend
  service**, which is separate from Staging and Prod services.
- **FR-004**: The Dev backend service MUST apply database migrations automatically, idempotently,
  and safely at startup against the **isolated Dev database** before serving traffic.
- **FR-004a**: After migrations, the Dev database MUST be idempotently seeded with reference and
  synthetic data (reusing the existing seeding pattern) so the environment is usable for
  validating user journeys; re-running the seed MUST NOT create duplicates or corrupt existing
  Dev data.
- **FR-005**: The pipeline MUST build the frontend and publish it to the **Dev frontend hosting**,
  configured to call the Dev backend API.
- **FR-006**: A release MUST pass a health/readiness check **and** the full end-to-end (E2E) test
  suite run against the Dev environment before it is promoted/declared successful; a release that
  fails either gate MUST NOT replace the currently running healthy release.
- **FR-007**: On any build, migration, health-check, or publish failure, the system MUST leave the
  previously running Dev release(s) (backend and frontend) intact and available.
- **FR-008**: The pipeline MUST report deployment status (success/failure, the deployed commit,
  and the resulting Dev version) on the GitHub Actions run and commit/deployment status for every
  deploy, and MUST additionally push a notification to the team chat channel on failure.
- **FR-009**: When multiple merges occur close together, the system MUST converge Dev on the
  latest merged commit rather than leaving an earlier commit live.
- **FR-010**: All environment-specific configuration and secrets for Dev MUST be supplied at
  deploy/run time from environment variables or a managed secret store; secrets MUST NOT be
  committed to the repository and MUST NOT be baked into container images.
- **FR-011**: Dev MUST use its own isolated database, identity configuration, and service
  configuration, separated from Staging and Prod.
- **FR-011a**: Access to Dev MUST be gated by the application's **existing identity model** — the
  same login/token scheme used in every other environment — behind the edge layer; no additional
  network restriction (IP allowlist/VPN) is required, and protected endpoints/actions MUST NOT be
  reachable without a valid Dev login. *(Note: the constitution §7 names Auth0 as the identity
  provider, but the codebase currently authenticates with an app-issued JWT bearer scheme. This
  pre-existing Auth0-vs-JWT divergence is out of scope here — this feature deploys Dev with the
  same auth the app already uses and must not be read as introducing or endorsing the divergence,
  which is tracked separately as a constitution/auth remediation.)*
- **FR-012**: The Dev backend MUST be Dockerized and deployed as a container; it MUST NOT require
  a developer's machine to remain running for Dev to stay available.
- **FR-013**: The Dev backend MUST expose health/readiness endpoints that the pipeline and
  platform use to gate promotion and report availability.
- **FR-014**: Error tracking and performance observability for Dev MUST be tagged with the Dev
  environment and the deployed release identifier, and MUST NOT capture secrets or sensitive
  HOA/homeowner/resident/property/violation/payment/document content.
- **FR-015**: Public Dev traffic to the backend API MUST be fronted by the project's edge/
  perimeter layer (DDoS protection, rate limiting, traffic filtering) consistent with the other
  environments.
- **FR-016**: The Dev environment MUST be cost-efficient when idle, consistent with the project's
  scale-to-zero posture for non-production (cold-start latency is acceptable for Dev).
- **FR-017**: Destructive migrations MUST have a documented rollback or mitigation plan before
  reaching Dev.
- **FR-018**: The pipeline MUST be repeatable and idempotent: re-running it for the same commit
  MUST produce the same Dev result without manual cleanup.

### Key Entities *(include if feature involves data)*

- **Deployment pipeline run**: A single automated execution triggered by a merge to `main`;
  attributes include triggering commit, build artifacts produced, target environment (Dev),
  status, and timestamps.
- **Backend container image**: The published, versioned/tagged backend artifact deployed to the
  Dev backend service.
- **Dev backend service**: The running, isolated Dev instance of the backend API (scales to zero
  when idle) with its own configuration and secrets.
- **Dev frontend deployment**: The published Dev build of the Angular application pointed at the
  Dev backend.
- **Dev database**: The isolated Dev datastore that receives automatic, idempotent migrations and
  is never shared with Staging/Prod.
- **Environment configuration / secret set**: The Dev-specific set of environment variables and
  managed secrets resolved at deploy/run time.

### Constitution Requirements *(mandatory when applicable)*

This is an infrastructure/CI-CD feature; product-data invariants (tenant boundary, file storage,
pagination, API envelope) are not directly exercised, but the following constitution gates apply:

- **CI/CD (Constitution §2, §9)**: Deployment MUST run automatically on merges to `main` via the
  project's GitHub Actions CI/CD; the Dev backend MUST be Dockerized and published to the project
  image registry; deploys go to the **isolated Dev** Cloud Run-class service.
- **Infrastructure & environments (Constitution §10)**: Dev MUST use a **separate** database and a
  **separate** backend service (and separate identity configuration as applicable) from Staging
  and Prod; frontend goes to Dev frontend hosting; edge/perimeter rules MUST align per environment.
- **Database/runtime (Constitution §3, §8)**: Strict migrations only (no manual DB edits);
  migrations MUST apply idempotently and safely at startup; destructive migrations require a
  rollback/mitigation plan; Dev DB access MUST use low max connections, pooling, and short-lived
  DbContext instances; Dev database MAY scale to zero.
- **Operations, secrets, and data lifecycle (Constitution §8)**: Secrets MUST NOT be committed or
  baked into images; Dev/Staging/Prod secrets MUST be isolated; backend MUST emit structured logs
  (Serilog) with correlation context; health/readiness endpoints MUST exist for the Dev service.
- **Observability (Constitution §8)**: Sentry MUST be configured for Dev with environment and
  release identifiers, trace context propagated across frontend/backend, and no capture of secrets
  or sensitive content.
- **Security and abuse controls (Constitution §7)**: Public Dev endpoints MUST sit behind the
  edge/perimeter layer with rate limiting and traffic filtering; configuration changes affecting
  identity/secrets are security-sensitive and SHOULD be auditable.
- **Quality gates (Constitution §9, §11)**: Required checks (lint, tests, static analysis,
  coverage, Repowise documentation refresh) MUST pass before a merge to `main`; the pipeline
  definition and supporting docs MUST be delivered as a focused vertical slice (or a justified
  cross-cutting infrastructure PR) and Repowise-maintained regions MUST be refreshed.
- **Swagger/OpenAPI (Constitution §4)**: Because Dev is a non-production developer environment,
  Swagger UI at `/swagger` MAY be enabled in Dev for debugging; it MUST remain disabled in Prod.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of merges to `main` automatically trigger a Dev deployment with zero manual
  steps.
- **SC-002**: A merged change is reflected in the running Dev environment (backend and frontend)
  within 30 minutes of merge under normal conditions, inclusive of the full E2E verification gate.
- **SC-003**: A failed deployment (build, migration, health-check, or E2E-suite failure) results
  in zero downtime of the previously running healthy Dev release — the prior version keeps serving
  100% of the time during the failed attempt.
- **SC-004**: 100% of schema changes merged to `main` are applied to the Dev database
  automatically, with no manual migration step.
- **SC-005**: Dev uses an isolated database and isolated secret set — 0 shared database
  connections or secret values with Staging or Prod, verifiable by configuration inspection.
- **SC-006**: 0 secret values are present in the repository or in deployed container image layers.
- **SC-007**: When the latest merge has deployed, the live Dev version corresponds to the most
  recent `main` commit 100% of the time (no stale-commit races left live).
- **SC-008**: Every Dev deployment's status and target version is observable to the team without
  asking the person who merged, and every failed deploy produces a team-chat notification.
- **SC-009**: After every successful deploy, the Dev database contains the expected reference/seed
  data, verifiable without manual data entry.
- **SC-010**: 100% of releases that fail the Dev E2E suite are blocked from being promoted to
  serve Dev traffic.

## Assumptions

- **Scope is the Dev environment only.** Setting up Staging and Prod auto-deploy is out of scope
  for this feature, though the pipeline SHOULD be designed so the same pattern can be extended to
  them later (per Constitution §10).
- **Both backend and frontend are in scope** for the Dev environment, since the constitution
  defines hosting for both and a Dev environment is only fully useful end to end.
- **Existing platform choices from the constitution are reused**: GitHub Actions for CI/CD, the
  project's container registry, the Cloud Run-class backend hosting (scale-to-zero), the managed
  PostgreSQL provider for the Dev database (scale-to-zero), Cloudflare Pages-class frontend
  hosting, Cloudflare edge in front of the API, the app's existing identity scheme, and Sentry for
  observability. This feature wires them together for Dev rather than selecting new technologies.
- **Identity is the app's existing JWT bearer scheme, not Auth0.** Although the constitution §7
  names Auth0, the codebase authenticates with an app-issued JWT. This feature deploys Dev with
  that same scheme (see FR-011a); reconciling the codebase to Auth0 is a separate, pre-existing
  concern outside this feature's scope.
- **Required pre-merge checks already exist** (lint, tests, static analysis, coverage, Repowise
  refresh) and gate merges to `main`; this feature consumes their pass/fail signal rather than
  redefining them.
- **Migrations are applied at backend startup** (idempotent), consistent with the constitution,
  rather than as a separate out-of-band step.
- **A managed secret store is available** for Dev secrets; provisioning the secret store itself is
  assumed to be an existing capability, and this feature defines which secrets Dev needs and how
  they are injected.
- **Cold-start latency is acceptable for Dev**, so scale-to-zero is preferred for cost efficiency.
- **"Push to Dev on merge to main" means deploy/release**, not a git push to another branch or
  repository.

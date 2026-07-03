# Tasks: Security Hardening Program (016)

**Input**: Design documents from `/specs/016-security-hardening/`
**Prerequisites**: plan.md, spec.md (+ sub-specs A–F), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. The constitution (§9, §2.4 Testing) mandates test-first (red→green), PostgreSQL/Testcontainers integration tests with transaction isolation, xUnit Theories for data-varied cases, and ≥95% coverage on changed files. Test tasks are written FIRST within each story and must fail before implementation.

**Organization**: Each sub-spec (A–F) is mapped to one user story / phase and is an **independently deliverable PR**. Order reflects priority + cross-slice dependency: US1=E and US2=F (Critical), US3=A, US4=C, US5=D (High), US6=B (Med–High).

| Story | Sub-spec | Deliver as |
|-------|----------|-----------|
| US1 | E — CI/CD & Infra Least Privilege | PR: least-privilege CI |
| US2 | F — AI Supply Chain | PR: agent hardening (needs US1 branch protection) |
| US3 | A — Identity & Access | PR: identity hardening |
| US4 | C — Platform & Data Protection | PR: platform/data protection |
| US5 | D — Frontend Session & Content Security | PR: frontend session (coordinate auth endpoints with US3) |
| US6 | B — Payments Integrity | PR: payments integrity |

## Format: `[ID] [P?] [Story] Description with file path`
- **[P]**: parallelizable (different files, no dependency on an incomplete task)
- **[US#]**: owning story (setup/foundational/polish have no story label)

---

## Phase 1: Setup (Shared)

- [ ] T001 Confirm the feature branch `016-security-hardening` and that `dotnet test` + `cd neko-hoa && npm ci` run green before changes (baseline).
- [ ] T002 [P] Establish a shared xUnit test category/collection for security regression tests in `HOAManagementCompany.Tests/Integration/Security/` (folder + fixture reuse of existing Testcontainers PostgreSQL fixture, transaction-per-test rollback).
- [ ] T003 [P] Add a `SecurityHardening` traceability note mapping each spec FR → planned test file at `specs/016-security-hardening/quickstart.md` (already drafted; keep updated as tests land).

## Phase 2: Foundational (Cross-slice ordering)

**Note**: The project is already initialized (existing .NET/Angular/infra). No new shared scaffolding is required. The only cross-slice dependency is captured here:

- [ ] T004 Deliver US1 (E) branch protection (T0xx) BEFORE US2 (F) merge-gate task — F FR-F2 relies on E's status-checks-only branch protection. Tracked in Dependencies.
- [ ] T005 [P] Recommend delivering US4 (C) PII-scrubbing enricher (T0xx) early so new security-event logging added by other slices is redacted at emission.

**Checkpoint**: Slices can proceed as independent PRs; observe the two ordering notes above.

---

## Phase 3: User Story 1 — E: CI/CD & Infra Least Privilege (Critical) 🎯 MVP

**Goal**: Remove project-Owner from CI identity, ref-scope apply, stop leaking secrets to PR-authored code, digest-pin the supply chain, and enforce status-checks branch protection.

**Independent Test**: Apply identity cannot be assumed from a non-`main` ref; deployer holds no `roles/owner`; PR plan/install/e2e steps have no write-capable secrets; every action is SHA-pinned; merge to `main` blocked without required checks; containers non-root.

### Tests / assertions for US1
- [ ] T006 [P] [US1] Add a CI-config assertion (script in `.github/scripts/assert-actions-pinned.py` or a workflow lint step) that fails if any `uses:` in `.github/workflows/*.yml` or `.github/actions/**` is not a 40-char SHA.
- [ ] T007 [P] [US1] Add an assertion that `.github/workflows/infra-plan.yml` and `pr-env.yml` define no job-level secret env (secrets only at step level) — script in `.github/scripts/assert-secret-scoping.py`.
- [ ] T008 [P] [US1] Add a Dockerfile lint assertion (all Dockerfiles declare a non-root `USER` and digest-pinned base) in `.github/scripts/assert-container-hardening.py`.

### Implementation for US1
- [ ] T009 [US1] Split the deployer identity in `infra/modules/environment/iam.tf`: replace the `roles/owner` grant (deployer_owner) with an enumerated least-privilege role set for the apply SA; add a read-only plan SA.
- [ ] T010 [US1] Ref-scope WIF in `infra/modules/environment/iam.tf`: add `attribute.ref` mapping and set the apply provider `attribute_condition` to require `assertion.ref == "refs/heads/main"`; the plan SA provider stays repo-scoped/any-ref.
- [ ] T011 [P] [US1] Move `TF_VAR_*`/Neon/Cloudflare secrets from job-level `env:` to step-level on the tofu steps only in `.github/workflows/infra-plan.yml` (lines ~28–38); plan job uses placeholder/read-only creds; gate behind a required-reviewer Environment.
- [ ] T012 [P] [US1] Scope secrets to tofu steps only (not `npm ci`/e2e) in `.github/workflows/pr-env.yml` (job `env:` ~45–62 → step env); same for `pr-env-teardown.yml`/`pr-env-sweep.yml`.
- [ ] T013 [P] [US1] SHA-pin all mutable-tag actions in `.github/workflows/infra-apply.yml`, `pr-env.yml`, `pr-env-teardown.yml`, `pr-env-sweep.yml`, `repowise.yml`, and `.github/actions/pr-env-tofu-init/action.yml`.
- [ ] T014 [P] [US1] Extend `.github/dependabot.yml` github-actions ecosystem to keep the new digest pins current.
- [ ] T015 [US1] Fix `.github/workflows/lock-merged-branch.yml`: remove the invalid `administration: write` permission; either use an appropriately-scoped credential or remove in favor of native branch protection (T016).
- [ ] T016 [US1] Configure `main` branch protection to require the defined status checks (test/frontend/integration/docker/sonar) with **no** mandated human review (FR-E7a accepted risk); record as-code where supported or document the dashboard config in `infra/` or `docs/`.
- [ ] T017 [P] [US1] Add advisory `CODEOWNERS` at repo root routing `.claude/**`, `infra/**`, `.github/**` reviews (advisory only under status-checks-only).
- [ ] T018 [P] [US1] Add non-root `USER` and digest-pinned base images to `Dockerfile`, `HOAManagementCompany/Dockerfile`, `neko-hoa/Dockerfile`; digest-pin images in `docker-compose.yaml` and bind management/data services to loopback.
- [ ] T019 [P] [US1] Make per-PR DB credentials distinct in `infra/modules/pr-environment/` (per-PR Neon role/password rather than a shared `neon_role_password`); update `pr-env.yml` accordingly.
- [ ] T020 [P] [US1] Pass the Sonar branch name via `env:` and reference it quoted in `.github/workflows/test.yml` (line ~54) instead of inline `${{ }}` interpolation.
- [ ] T021 [US1] Refresh Repowise marker regions for changed infra/CI files; run the repowise workflow locally (no-op if unchanged).

**Checkpoint**: US1 independently testable and deliverable.

---

## Phase 4: User Story 2 — F: AI Supply Chain (Critical)

**Goal**: Make untrusted content data-not-instructions for every agent; remove the local arbitrary-command bypass; pin/verify agent tooling; constrain the autonomous merge routine.

**Independent Test**: No `Bash(rtk proxy *)` allow entry; deny list blocks passthrough + `.claude/**` writes; installer pinned+checksum-verified (fails closed); CLAUDE.md says "verify before acting"; merge routine acts only on structured metadata.

**Depends on**: US1 T016 (branch protection) for FR-F2.

### Tests / assertions for US2
- [ ] T022 [P] [US2] Add an assertion (script `.github/scripts/assert-agent-config.py`) that `.claude/settings.local.json`/global contain no `Bash(rtk proxy *)` allow entry and DO contain the required deny entries.
- [ ] T023 [P] [US2] Add a shell test for `.claude/hooks/rtk-install.sh` verifying it refuses to install when the downloaded artifact's checksum does not match the pinned value (fail-closed).

### Implementation for US2
- [ ] T024 [US2] Remove `Bash(rtk proxy *)` from `.claude/settings.local.json` allow list (and the global `~/.claude/settings.json` if present in-repo scope); document the removal.
- [ ] T025 [US2] Add a minimal targeted deny list to `.claude/settings.local.json` (deny arbitrary passthrough and writes to `.claude/**`); confirm deny precedence.
- [ ] T026 [US2] Pin `.claude/hooks/rtk-install.sh` to an immutable rtk version + verify a published checksum/signature before executing; replace the `curl … master | sh` pattern; fail closed on mismatch.
- [ ] T027 [P] [US2] Constrain `.claude/hooks/rtk-hook.sh` so the rewrite output is limited to known-safe wrapper prefixes (or surfaced for inspection) rather than executed as opaque trusted output.
- [ ] T028 [P] [US2] Reword `.claude/CLAUDE.md`: elevate "always verify against actual source before making changes"; remove the "trust the response and act on it" instruction for Repowise/tool/indexed content.
- [ ] T029 [P] [US2] Confirm ownership of `ANTHROPIC_BASE_URL` `127.0.0.1:8787`, restrict access, and document it (as dev-only) in `.claude/` or `docs/`; keep the proxy per clarification.
- [ ] T030 [P] [US2] Ensure `CODEOWNERS` (T017) covers `.claude/**` so agent-config changes are review-routed.
- [ ] T031 [US2] (Out-of-repo) Reconfigure the cloud Dependabot merge routine: decide only from `gh pr view --json ...` metadata, assert `author == dependabot[bot]`, restrict to patch/minor, enable notifications, never parse PR body/changelog as instructions. Record the end-state in `specs/016-security-hardening/spec-ai-supply-chain.md` and note the dashboard change is applied.

**Checkpoint**: US2 independently testable; merge-routine end-state documented.

---

## Phase 5: User Story 3 — A: Identity & Access (High)

**Goal**: Prevent property-claim takeover (email-verification gate + single-use 90-day claim code), enable per-account lockout, pin JWT alg, throttle registration, secret-gate e2e cleanup.

**Independent Test**: Guessed account number without verified email + valid claim code cannot claim a property; 10 failed logins → 30-min lock; non-HS256 token rejected; enumeration probes return uniform responses; e2e cleanup refused without secret.

### Tests for US3 (write first, must fail)
- [ ] T032 [P] [US3] Testcontainers integration Theory for property-claim: (verified email + valid code) succeeds; (no verified email) / (bad/expired/used code) / (guessed account number only) all refused — `HOAManagementCompany.Tests/Integration/Security/PropertyClaimTests.cs`.
- [ ] T033 [P] [US3] Theory for lockout: 10 failures → locked 30 min; correct password during lock refused; independent of IP — `.../Security/LoginLockoutTests.cs`.
- [ ] T034 [P] [US3] Theory for enumeration: `/auth/verify-email/request` and register return uniform responses for known vs unknown emails/accounts — `.../Security/AuthEnumerationTests.cs`.
- [ ] T035 [P] [US3] Test JWT validation rejects non-HS256 alg and >30s-expired tokens — `.../Security/JwtValidationTests.cs`.
- [ ] T036 [P] [US3] Test `E2ECleanupEndpoint` refuses without `X-Scheduler-Secret` and is unavailable in production-like env — `.../Security/E2ECleanupGateTests.cs`.

### Implementation for US3
- [ ] T037 [P] [US3] Add `EmailVerification` entity + config in `HOAManagementCompany/Domain/Entities/` and `Infrastructure/Persistence/ApplicationDbContext.cs` (fields per data-model.md); create strict EF migration.
- [ ] T038 [P] [US3] Add `PropertyClaimCode` entity + config (hashed code, 90-day expiry, single-use, attempt count); create strict EF migration; verify migration on PostgreSQL.
- [ ] T039 [US3] Implement email-verification endpoints (`/auth/verify-email/request`, `/auth/verify-email/confirm`) per `contracts/property-claim.md` in `Features/Auth/`; uniform responses; hashed codes; attempt limits.
- [ ] T040 [US3] Rework `Features/Auth/AuthService.cs` `RegisterAsync` (+ `RegisterEndpoint.cs`) to require a verification token then a valid single-use claim code before property binding; account-number match alone insufficient.
- [ ] T041 [US3] Enable Identity lockout in `Program.cs` `AddIdentityCore` (MaxFailedAccessAttempts=10, DefaultLockoutTimeSpan=30 min, config-driven + FluentValidation-validated) and route `LoginAsync` through the lockout-aware path.
- [ ] T042 [P] [US3] Pin `ValidAlgorithms=[HS256]` and `ClockSkew=30s` in `Program.cs` `TokenValidationParameters`.
- [ ] T043 [P] [US3] Apply rate limiting to `RegisterEndpoint` and the verify-email endpoints (reuse `auth` policy / trusted-edge partition via `ClientIdentityResolver`).
- [ ] T044 [P] [US3] Require `X-Scheduler-Secret` (constant-time compare) + non-production gate on `E2ECleanupEndpoint`.
- [ ] T045 [P] [US3] Remove committed dev/test JWT secrets from `appsettings.Development.json`/`appsettings.Test.json` in favor of user-secrets/env; keep production sourcing from the secret store.
- [ ] T046 [US3] Add scrubbed security-event logging for claim attempts, verification, and lockout (coordinate with US4 enricher).
- [ ] T047 [US3] Refresh Repowise markers for changed auth files.

**Checkpoint**: US3 independently testable and deliverable.

---

## Phase 6: User Story 4 — C: Platform & Data Protection (High)

**Goal**: Wire the PII scrubber into logs, clamp pagination, fix telemetry limiter + add global default, generic error+correlation ID on deployed non-local, verified email change + input caps, security headers.

**Independent Test**: emitted log events show `[REDACTED]`; extreme page sizes clamped (no 500/full scan); Dev 500 returns generic+correlationId; profile oversize rejected; responses carry header baseline.

### Tests for US4 (write first, must fail)
- [ ] T048 [P] [US4] Integration test asserting the scrubbing enricher is registered and `{Email}` is redacted in emitted log events — `HOAManagementCompany.Tests/Integration/Security/LogScrubbingTests.cs`.
- [ ] T049 [P] [US4] Theory for pagination bounds (PageSize huge, Page overflow) → clamped, no server error — `.../Security/PaginationBoundsTests.cs`.
- [ ] T050 [P] [US4] Test deployed-non-local error shape = generic message + correlationId; local Development shows detail — `.../Security/ErrorShapeTests.cs`.
- [ ] T051 [P] [US4] Test verified email change (no immediate change; applies after confirm) + profile length/E.164 validation + over-posting rejected — `.../Security/OwnerProfileTests.cs`.
- [ ] T052 [P] [US4] Test security-header baseline present on responses — `.../Security/SecurityHeadersTests.cs`.

### Implementation for US4
- [ ] T053 [US4] Register `TelemetryScrubbingEnricher` in `Program.cs` Serilog setup (`AddSingleton<ILogEventEnricher>` so `ReadFrom.Services` composes it).
- [ ] T054 [P] [US4] Add FluentValidation `Validator<T>` clamping Page≥1, 1≤PageSize≤100 for the four Community list requests in `Features/Community/Models/CommunityModels.cs`; overflow-safe `Skip` in `CommunityService.cs`.
- [ ] T055 [P] [US4] Partition the `telemetry` rate-limit policy by `ClientIdentityResolver.ResolveAuthPartition` in `Program.cs`; add a global default limiter for un-policied endpoints (incl. registration).
- [ ] T056 [US4] Change `Features/Common/GlobalExceptionHandler.cs` to return generic message + correlationId for deployed non-local; full detail only local Development; production preserved.
- [ ] T057 [US4] Implement verified email change in `Features/Property/PropertyService.cs`/`OwnerPatchEndpoint.cs` (reuse `EmailVerification` purpose=email_change); apply on confirm; sync identity store.
- [ ] T058 [P] [US4] Add MaximumLength + E.164 phone validation to the owner-patch DTO validator; keep privileged fields non-editable.
- [ ] T059 [P] [US4] Add security-headers middleware to the `Program.cs` pipeline (nosniff, frame options, HSTS where applicable).
- [ ] T060 [US4] Refresh Repowise markers for changed platform files.

**Checkpoint**: US4 independently testable and deliverable.

---

## Phase 7: User Story 5 — D: Frontend Session & Content Security (High)

**Goal**: Move the refresh token to an HttpOnly cookie (full end-state), enforce a repo-controlled CSP, scope the interceptor + single-flight refresh, remove committed auth-state.

**Independent Test**: refresh token not script-readable; enforcing CSP served; bearer only on API-origin; no tracked real token; e2e still passes.

**Coordinate with**: US3 (both touch `Features/Auth` Login/Refresh) — sequence after US3 to avoid conflicts.

### Tests for US5 (write first, must fail)
- [ ] T061 [P] [US5] Backend integration test: `/auth/login` sets `HttpOnly; Secure; SameSite` refresh cookie and omits refresh token from the body; `/auth/refresh` reads cookie, rotates, clears on logout — `HOAManagementCompany.Tests/Integration/Security/AuthCookieTests.cs`.
- [ ] T062 [P] [US5] Karma unit test: `token.service` holds access token in memory, exposes no refresh token to storage; `isTokenExpired` handles base64url — `neko-hoa/src/app/core/services/token.service.spec.ts`.
- [ ] T063 [P] [US5] Karma test: `auth.interceptor` attaches bearer only for `environment.apiBaseUrl` and does single-flight refresh — `neko-hoa/src/app/core/interceptors/auth.interceptor.spec.ts`.
- [ ] T064 [P] [US5] Cypress e2e: login → reload → silent refresh restores session; payments/API work under enforcing CSP — `neko-hoa/cypress/e2e/session-security.cy.ts`.

### Implementation for US5
- [ ] T065 [US5] Backend: set `HttpOnly; Secure; SameSite` refresh cookie in `Features/Auth/LoginEndpoint.cs`; read refresh from cookie (not body) in `RefreshEndpoint.cs`; clear on logout — per `contracts/auth-session.md`.
- [ ] T066 [US5] Frontend `token.service.ts`: remove `neko_refresh`/`neko_user` from localStorage; hold access token in memory (signal); fix base64url in `isTokenExpired`.
- [ ] T067 [US5] Frontend `auth.service.ts`: remove localStorage token persistence; restore session via silent refresh.
- [ ] T068 [US5] Add a silent-refresh `APP_INITIALIZER` in `neko-hoa/src/main.ts` (after config guard, before render).
- [ ] T069 [P] [US5] `auth.interceptor.ts`: attach bearer only when `req.url` starts with `environment.apiBaseUrl`; implement single-flight refresh; omit refresh body (cookie carries it).
- [ ] T070 [P] [US5] Create `neko-hoa/src/assets/_headers` with an **enforcing** CSP (script/frame: self + `js.stripe.com`; connect: self + API origin; other baseline headers); verify `angular.json` copies it into `dist/neko-hoa/browser`.
- [ ] T071 [P] [US5] `git rm --cached neko-hoa/e2e/.auth/state.json`; add `e2e/.auth/` to `neko-hoa/.gitignore`; invalidate the committed dev refresh token; confirm `e2e/global-setup.ts` regenerates state.
- [ ] T072 [P] [US5] Fix opener ordering in `neko-hoa/src/app/features/community/documents/documents.component.ts` (use `window.open(url,'_blank','noopener,noreferrer')`).
- [ ] T073 [US5] Refresh Repowise markers for changed frontend/auth files.

**Checkpoint**: US5 independently testable and deliverable.

---

## Phase 8: User Story 6 — B: Payments Integrity (Med–High)

**Goal**: Make deferred settlement atomic (no double-credit), add durable uniqueness backstop, per-tenant idempotency, amount-mismatch review queue. Forward-only.

**Independent Test**: crash/redelivery/concurrent settlement → exactly one credit; duplicate (TransactionId,EntryType) rejected; cross-tenant idempotency key ok; mismatch → no credit + queue row.

### Tests for US6 (write first, must fail)
- [ ] T074 [P] [US6] Testcontainers Theory: settlement under simulated crash-then-reprocess (redelivery + reconciliation) and concurrent processing → exactly one ledger credit — `HOAManagementCompany.Tests/Integration/Security/SettlementAtomicityTests.cs`.
- [ ] T075 [P] [US6] Test the `(TransactionId, EntryType)` unique backstop rejects a second settlement credit but allows a distinct refund entry — `.../Security/LedgerUniquenessTests.cs`.
- [ ] T076 [P] [US6] Test per-tenant idempotency: same key across two properties both succeed; same-property replay returns original (no 500) — `.../Security/IdempotencyIsolationTests.cs`.
- [ ] T077 [P] [US6] Test amount mismatch → no credit + `SettlementReviewQueue` row (status open) — `.../Security/AmountMismatchTests.cs`.

### Implementation for US6
- [ ] T078 [US6] Verify the current transaction boundary in `Features/Payments/Webhooks/WebhookProcessor.cs` `SettleSucceededAsync` (:73–89) and `LedgerService.Append`; confirm the append opens its own transaction when none is ambient (the double-credit root cause).
- [ ] T079 [US6] Wrap the settlement status flip + `LedgerService.Append` + receipt in one `CreateExecutionStrategy().ExecuteAsync` + `BeginTransactionAsync` in `WebhookProcessor.SettleSucceededAsync`, mirroring `ConfirmPaymentEndpoint`.
- [ ] T080 [P] [US6] Add the unique index `LedgerEntries (TransactionId, EntryType)` in `ApplicationDbContext.cs`; strict migration; pre-check for existing duplicates (do not auto-delete — surface if the index creation fails).
- [ ] T081 [P] [US6] Change the idempotency unique index to composite `(PropertyId, IdempotencyKey)` in `ApplicationDbContext.cs`; strict migration (drop+recreate; pre-check cross-tenant collisions; rollback note); catch unique-violation → replay-collapse in the confirm path.
- [ ] T082 [P] [US6] Add `SettlementReviewQueue` entity + config + strict migration (fields per data-model.md).
- [ ] T083 [US6] Add the provider-vs-expected amount cross-check on settlement; on mismatch block the credit and enqueue a `SettlementReviewQueue` row.
- [ ] T084 [US6] Refresh Repowise markers for changed payments files.

**Checkpoint**: US6 independently testable and deliverable.

---

## Phase 9: Polish & Cross-Cutting

- [ ] T085 [P] Verify ≥95% coverage on changed files per slice (Codecov gate) and that all new tests pass alongside the existing suite.
- [ ] T086 [P] Run each slice through `specs/016-security-hardening/quickstart.md` and record pass/fail per FR.
- [ ] T087 Update the umbrella `spec.md` finding inventory to `fixed` / `accepted-risk` states as slices merge; keep sub-spec `spec.md` files truthful (Constitution §11 living specs).
- [ ] T088 [P] Ensure each slice PR is scoped to one sub-spec (Constitution §12 vertical/horizontal slice) and refreshes Repowise markers (no-op if unchanged).
- [ ] T089 [P] Confirm no live secrets remain tracked (repo scan) after US1/US3/US5 changes (SC-003).
- [ ] T090 Re-run an independent security re-review against the merged slices to confirm zero prior Critical/High findings reproduce (SC-005).

---

## Dependencies & Execution Order

- **Setup (Phase 1)** → **Foundational (Phase 2)** → stories.
- **US1 (E)** first; **US1 T016 (branch protection)** blocks **US2 T031/FR-F2**.
- **US2 (F)** after US1 branch protection.
- **US3 (A)** independent; **US5 (D)** touches the same `Features/Auth` login/refresh files → sequence US5 after US3 to avoid merge conflicts.
- **US4 (C)** independent; deliver early so its PII-scrubbing enricher (T053) benefits other slices' logging.
- **US6 (B)** fully independent (payments subsystem).
- Within a story, `[P]` tasks touch different files and can run in parallel; migrations within a story are sequential (shared snapshot file).

## Parallel Execution Examples
- **US1**: T006/T007/T008 (assertions) in parallel; T011/T012/T013/T014 (distinct workflow files) in parallel after T009/T010.
- **US3**: T032–T036 (tests, distinct files) in parallel; T037/T038 (distinct entities) in parallel before T040.
- **US4**: T048–T052 in parallel; T054/T055/T058/T059 in parallel after T053.
- **US6**: T074–T077 in parallel; T080/T081/T082 migrations sequential (shared snapshot).

## Implementation Strategy
- **MVP = US1 (E)** — the Critical blast-radius fix; de-fangs several other findings.
- Then **US2 (F)** (needs US1), then the High app slices **US3/US4/US5**, then **US6 (B)**.
- Each story is a standalone PR gated by required checks; merge order follows the dependencies above.

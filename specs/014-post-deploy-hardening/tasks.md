---

description: "Task list template for feature implementation"
---

# Tasks: Hardening not addressed by ephemeral environments

**Input**: Design documents from `/specs/014-post-deploy-hardening/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included. The spec's Quality Gates and the constitution make these tests part of the completion gate (per-client isolation, forged-header rejection, config-gating). Write tests first where the testing constitution applies.

**Organization**: Tasks are grouped by user story so each is independently implementable and testable. The three slices are orthogonal — US1 (P1) is the MVP and the only end-user-facing fix.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3
- All paths are repo-relative.

## Path Conventions

- Backend: `HOAManagementCompany/`, tests in `HOAManagementCompany.Tests/`
- Frontend e2e: `neko-hoa/`
- CI: `.github/workflows/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a known-green baseline before changes.

- [ ] T001 Confirm baseline is green: run `dotnet build` and `dotnet test` from repo root, and `npm ci` in `neko-hoa/`; note current pass state so regressions are attributable.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-story prerequisites.

**Note**: This feature has **no blocking foundational work** — US1, US2, and US3 touch disjoint files and are fully independent. Proceed directly to the user-story phases. (Phase intentionally contains no tasks.)

**Checkpoint**: Foundation ready — all three user stories may begin in parallel.

---

## Phase 3: User Story 1 - Per-client API rate limiting (Priority: P1) 🎯 MVP

**Goal**: Replace the global, partition-less `auth`/`payments` limiters with partitioned policies — `auth` keyed by trusted client IP (`CF-Connecting-IP`), `payments` keyed by authenticated user identity, un-attributable requests isolated in a shared `"unknown"` bucket — with env-tunable thresholds and forged-header resistance.

**Independent Test**: Run `dotnet test --filter FullyQualifiedName~RateLimitingTests`; confirm one client exhausting its quota does not throttle another, forged `CF-Connecting-IP` is ignored, verified-edge requests partition by true client IP, and thresholds honor config. (Maps to spec US1 #1–#4, SC-001/002/003; contract `contracts/rate-limiting-behavior.md`.)

### Tests for User Story 1 ⚠️ (write first, ensure they FAIL)

- [ ] T002 [US1] Write integration tests in `HOAManagementCompany.Tests/Integration/RateLimitingTests.cs` using `WebApplicationFactory`, covering RL-1…RL-6 from `contracts/rate-limiting-behavior.md`: per-client isolation for `auth` (by IP) and `payments` (by user), forged-header → `"unknown"`, verified-edge → true-IP partition, `"unknown"` isolation, NAT-shared users independent payment windows, and config-driven thresholds (use xUnit Theory for header-trust variations; ensure parallel/rerun-safe with no shared state).

### Implementation for User Story 1

- [ ] T003 [P] [US1] Create `HOAManagementCompany/Infrastructure/Configuration/RateLimitingOptions.cs` (with nested `TrustedEdgeOptions`) bound from the `"RateLimiting"` section, fields per `data-model.md` (`AuthPermitsPerMinute`, `PaymentsPermitsPerMinute`, `UnknownPermitsPerMinute`, `TrustedEdge.SecretHeaderName/SecretHeaderValue`).
- [ ] T004 [P] [US1] Create a FluentValidation validator for `RateLimitingOptions` (all `*PermitsPerMinute` ≥ 1; `TrustedEdge` secret name/value both-or-neither), registered with the existing fail-fast startup validation (008-config-validation pattern).
- [ ] T005 [P] [US1] Create `HOAManagementCompany/Infrastructure/RateLimiting/ClientIdentityResolver.cs` that resolves: trusted client IP from `CF-Connecting-IP` only when the configured edge secret header is present and matches (else `"unknown"`); and the authenticated user identity from `HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? HttpContext.User.FindFirst("sub")?.Value` (else `"unknown"`) — the same subject-claim pattern used by `MeEndpoint`/`LogoutEndpoint`/`TraceEnrichmentMiddleware` (Q1). Add a `// <!-- REPOWISE:START domain=rate-limiting -->` marker region.
- [ ] T006 [US1] Rewrite the rate-limiter registration in `HOAManagementCompany/Program.cs` (~lines 217–248): replace `AddFixedWindowLimiter("auth")` and `AddFixedWindowLimiter("payments")` with partitioned `AddPolicy(...)` policies using `ClientIdentityResolver` (auth→IP, payments→user, fallback→`"unknown"`) and the configured permit limits; keep `RejectionStatusCode = 429`, `QueueLimit = 0`, 1-minute window; leave the `telemetry` policy unchanged. (Depends on T003, T005.) **Verify pipeline ordering (V1):** confirm `app.UseRateLimiter()` (~L365) runs **after** `UseAuthentication`/`UseAuthorization` (~L352–353) so `HttpContext.User` is populated when the `payments` policy reads the subject claim; add a brief inline comment asserting this ordering invariant.
- [ ] T007 [US1] Set `RateLimiting` defaults per environment in `HOAManagementCompany/appsettings.json` (conservative prod: auth 20, payments 20, unknown 30) and `appsettings.Dev.json`/Dev config (raised auth/payments to keep Playwright login bursts from 429); document that `TrustedEdge` secret is injected from Cloud Run secret config, never committed.
- [ ] T008 [US1] Run `dotnet test --filter FullyQualifiedName~RateLimitingTests` to green; execute the US1 section of `quickstart.md` to verify behavior end-to-end.

**Checkpoint**: US1 is the deliverable MVP — per-client limiting is live and verified.

---

## Phase 4: User Story 2 - Curated post-deploy smoke gate (Priority: P2)

**Goal**: The post-deploy gate runs only a small, read-only, deterministic `@smoke` subset instead of the full Playwright suite; the full regression suite stays runnable for local/PR.

**Independent Test**: Run `npm run e2e:playwright-smoke` against a target — confirm only `@smoke` checks run, it finishes quickly, leaves no created accounts / toggled enrollment, and still fails when auth is down or key pages do not render. `npm run e2e` still runs the full suite. (Maps to spec US2 #1–#5, SC-004/005; contract `contracts/smoke-gate.md`.)

### Implementation for User Story 2

- [ ] T009 [US2] Curate the smoke set in `neko-hoa/e2e/`: create `smoke.spec.ts` (or tag existing read-only checks) with Playwright `{ tag: '@smoke' }` covering only read-only deployment-health checks per `contracts/smoke-gate.md` (login/portal renders, authenticated dashboard + key pages render, API reachable). Do NOT tag registration, auto-pay toggle, poll-vote, RSVP, payment submission, or `@local-only` specs.
- [ ] T010 [P] [US2] Add `"e2e:playwright-smoke": "playwright test --grep @smoke"` to `neko-hoa/package.json`; keep `e2e` (full suite) available for local/PR use (FR-007). **Resolve the old `e2e:playwright-dev` script (I1):** since the post-deploy gate moves to `e2e:playwright-smoke` (T011), either remove `e2e:playwright-dev` or repurpose it as a documented "full suite vs Dev" local script — do **not** leave a second post-deploy entry point; the plan summary's `e2e:playwright-dev → e2e:playwright-smoke` wording reflects this single-gate intent.
- [ ] T011 [US2] Update `.github/workflows/test.yml` "Playwright smoke suite" step (~line 339–346) to run `npm run e2e:playwright-smoke` instead of `npm run e2e:playwright-dev`.
- [ ] T012 [US2] Verify SM-1…SM-5: run the smoke gate twice against a shared target, confirm 0 state-mutating tests and identical owned-data before/after (SC-005), confirm it fails loudly when a key page/auth is broken (inject a failure), and confirm the full suite still runs via `npm run e2e`.
- [ ] T012a [US2] Measure and assert the smoke-gate runtime against SC-004: capture wall-clock duration of `npm run e2e:playwright-smoke` vs the full `npm run e2e` suite (one-time baseline) and record that the smoke gate completes in a small fraction of the full suite's time; document the figures in this file's Notes (G1).

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Config-gated environment behavior (Priority: P3)

**Goal**: Drive exception-detail exposure and SQL-text capture from explicit config defaulting to `StartupOptions.IsDevLike(...)` (true for local `Development` and deployed `Dev`), hard-off in `Production`; eliminate remaining host-name gates that should also apply to `Dev`.

**Independent Test**: Run `dotnet test --filter FullyQualifiedName~DebugGatingTests`; confirm exception `detail` and SQL-text are ON in `Development`/`Dev` by default, OFF in `Production`, and a `Production` override cannot enable them. (Maps to spec US3 #1–#3, SC-006/007; contract `contracts/debug-gating-behavior.md`.)

### Tests for User Story 3 ⚠️ (write first, ensure they FAIL)

- [ ] T013 [US3] Write `HOAManagementCompany.Tests/Integration/DebugGatingTests.cs` covering DG-1…DG-8 from `contracts/debug-gating-behavior.md`: exception `detail` populated in `Dev`/`Development`, `null` in `Production`, `null` when `Production` attempts an override, `null` when `Dev` explicitly disables; and `CaptureSqlText` true in `Dev`, false in `Production`, explicit value honored (build the host per environment with config overrides).

### Implementation for User Story 3

- [ ] T014 [P] [US3] Add `ExposeExceptionDetail` to `HOAManagementCompany/Infrastructure/Configuration/DevToolsOptions.cs` (create the options class if not present, bound from `"DevTools"`, alongside the existing `E2ECleanupEnabled`), default `StartupOptions.IsDevLike(env)`, forced `false` in `Production` (mirror the Swagger invariant in `StartupOptions.Resolve`).
- [ ] T014a [P] [US3] **(Constitution §8 — C1)** Create and register a FluentValidation validator for `DevToolsOptions` so the new config class is bound-and-validated at startup with fail-fast behavior, matching the 008-config-validation pattern (and the `RateLimitingOptions` validator in T004). New configuration MUST ship with its validator. (Depends on T014.)
- [ ] T015 [US3] Update `HOAManagementCompany/Features/Common/GlobalExceptionHandler.cs` to populate `detail` from `DevToolsOptions.ExposeExceptionDetail` instead of `env.IsDevelopment()` (inject the resolved options); keep the `{ code, message, detail }` response shape and `ScrubbedKeys`/redaction unchanged. Add a `// <!-- REPOWISE:START domain=error-handling -->` marker.
- [ ] T016 [P] [US3] Update `HOAManagementCompany/Infrastructure/Observability/ObservabilityOptions.cs` (~line 73) so the `CaptureSqlText` default derives from `StartupOptions.IsDevLike(environment)` instead of `environment.IsDevelopment()`; explicit `Observability:CaptureSqlText` still wins.
- [ ] T017 [US3] Wire `DevToolsOptions` resolution/registration in `HOAManagementCompany/Program.cs` and add `DevTools`/relevant defaults to `appsettings*.json` (no `ExposeExceptionDetail` value needed in prod — invariant forces off).
- [ ] T018 [US3] Audit residual host-name gates: run `grep -rn "IsDevelopment()" HOAManagementCompany --include=*.cs`, convert any behavior that should also apply to deployed `Dev` to `IsDevLike`/config, and record the audit result (remaining genuinely-Development-only hits) in this file's Notes to demonstrate SC-006 (0 silent `Dev` no-ops).
- [ ] T019 [US3] Run `dotnet test --filter FullyQualifiedName~DebugGatingTests` to green; execute the US3 section of `quickstart.md`.

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T020 [P] Repowise review: ensure marker regions added in T005/T015 (and `RateLimitingOptions`) contain regenerated or confirmed-unchanged content; indexed docs match merged code.
- [ ] T021 Security/rate-limit review: confirm forged-header resistance, `"unknown"` fail-safe (never falls back to proxy address), and that exception detail / SQL text remain off in Production by default (SC-007); no secret/PII in `detail`.
- [ ] T022 [P] Verify Codecov ≥ 95% for changed/added backend files (`RateLimitingOptions`, `ClientIdentityResolver`, `GlobalExceptionHandler`, `ObservabilityOptions`, validators) and that Sonar PR scan passes.
- [ ] T023 Run full `dotnet test` and the relevant `neko-hoa` suites; confirm no regression introduced by the limiter/handler changes.
- [ ] T024 **Before the PR**: bring this feature's `spec.md` and `tasks.md` up to date with the work actually performed (final threshold values, exact smoke-set membership, audit outcome); update any older `spec.md` that drifted. `plan.md`/`research.md` need not be refreshed.
- [ ] T025 Verify the spec stays executable: every mandatory US1/US2/US3 acceptance scenario and FR maps to a currently-passing automated test (RL-*, SM-*, DG-*); no spec claim left unverified.
- [ ] T026 Reconcile cross-spec consistency: confirm this feature extends (does not contradict) the 009 `StartupOptions`/`DevTools` config-flag pattern and the 014 post-deploy CI flow; record which spec prevails for any overlap.
- [ ] T027 Run `quickstart.md` validation end-to-end across all three slices.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Empty — does not block.
- **User Stories (Phases 3–5)**: Each depends only on Setup; mutually independent and parallelizable.
- **Polish (Phase 6)**: After the desired stories are complete.

### User Story Dependencies

- **US1 (P1)**: Independent. MVP.
- **US2 (P2)**: Independent (frontend/CI only; no backend dependency).
- **US3 (P3)**: Independent (backend debug-gating only).

### Within Each User Story

- Tests written first and failing before implementation (US1: T002 before T003–T008; US3: T013 before T014–T019).
- Options/validators and resolver (different files, [P]) before the `Program.cs` wiring that consumes them.

### Parallel Opportunities

- Because the three stories touch disjoint files, US1, US2, and US3 can be developed fully in parallel by different people after Setup.
- Within US1: T003, T004, T005 are [P] (different files); T006 depends on T003+T005.
- Within US3: T014 and T016 are [P] (different files); T015 depends on T014.

---

## Parallel Example: User Story 1

```bash
# After writing the failing tests (T002), build the independent pieces in parallel:
Task: "Create RateLimitingOptions.cs in HOAManagementCompany/Infrastructure/Configuration/"
Task: "Create RateLimitingOptions validator (FluentValidation)"
Task: "Create ClientIdentityResolver.cs in HOAManagementCompany/Infrastructure/RateLimiting/"
# Then converge: rewrite the limiter registration in Program.cs (T006).
```

## Parallel Example: Across Stories

```bash
# Developer A → US1 (backend rate limiting)
# Developer B → US2 (Playwright @smoke + CI)
# Developer C → US3 (config-gated debug behavior)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup → known-green baseline.
2. Phase 3 US1 → per-client rate limiting (the only end-user-facing fault).
3. **STOP and VALIDATE**: `RateLimitingTests` green + quickstart US1.
4. Ship — this alone resolves the production self-DoS.

### Incremental Delivery

1. US1 → test → ship (MVP).
2. US2 → test → ship (smoke gate trustworthy, no shared-state pollution).
3. US3 → test → ship (Dev debuggability restored, prod posture unchanged).

---

## Notes

- [P] = different files, no incomplete dependencies.
- No database/schema/migration changes; no new dependency.
- T018 audit output (residual `IsDevelopment()` classification) to be recorded here before the PR to evidence SC-006.
- Final production threshold values (auth/payments/unknown per minute) are tunable config; confirm in T007/T024 (analysis Q2 — acceptable as deferred config).
- T012a smoke-vs-full-suite timing figures to be recorded here before the PR to evidence SC-004 (analysis G1).
- Payments partition key = subject claim (`ClaimTypes.NameIdentifier` → `sub` fallback), per T005 (analysis Q1).
- `/speckit.analyze` remediation folded in: **C1**→T014a (DevToolsOptions validator), **G1**→T012a (timing assertion), **Q1**→T005 (claim pinned), **I1**→T010 (single post-deploy gate), **V1**→T006 (pipeline-order check), **Q2**→deferred config note above, **S1/T1**→spec scope note + plan testing note.

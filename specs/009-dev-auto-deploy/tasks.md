---
description: "Task list for 009-dev-auto-deploy"
---

# Tasks: Dev Environment Auto-Deploy on Merge to Main

**Input**: Design documents from `/specs/009-dev-auto-deploy/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: This is a CI/CD + configuration feature. Test tasks are included only where the
constitution's completion gate applies — the backend startup-config refactor (xUnit) and the
**E2E suite that gates promotion**. No CRUD model/endpoint tests apply (no new entities/endpoints).

**Organization**: Tasks are grouped by user story. US1 (backend deploy) is the MVP; US2 adds the
frontend; US3 makes deploys safe; US4 verifies isolation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US4 (user-story phases only)
- All paths are repository-relative absolute from repo root.

## Path Conventions

- Backend: `HOAManagementCompany/`, tests `HOAManagementCompany.Tests/`
- Frontend: `neko-hoa/`
- Pipeline: `.github/workflows/test.yml` (existing CI — the `deploy-dev` job is appended here)
- Platform provisioning is tracked in `specs/009-dev-auto-deploy/quickstart.md` and
  `contracts/environment-matrix.md`

---

## Phase 1: Setup (Platform Provisioning & CI Secrets)

**Purpose**: One-time platform resources the pipeline deploys into. No application code. See
`quickstart.md` "One-time provisioning".

- [ ] T001 Provision isolated **Neon Dev** database (scale-to-zero, low `Maximum Pool Size`); record the connection target in `specs/009-dev-auto-deploy/contracts/environment-matrix.md`
- [ ] T002 [P] Provision Cloudflare **R2 Dev** documents bucket + scoped access/secret keys; record in `specs/009-dev-auto-deploy/contracts/environment-matrix.md`
- [ ] T003 [P] Create Cloud Run service `nekohoa-api-dev` (project region, `min-instances=0`, scale-to-zero); note service name in `specs/009-dev-auto-deploy/quickstart.md`
- [ ] T004 [P] Create Cloudflare **Pages Dev** project and map proxied `api-dev.nekohoa.com` → `nekohoa-api-dev`; note in `specs/009-dev-auto-deploy/quickstart.md`
- [ ] T005 Create **Google Secret Manager** entries for every Dev runtime secret in `specs/009-dev-auto-deploy/contracts/environment-matrix.md` (`ConnectionStrings__DefaultConnection`, `Jwt__Secret`, `Sentry__Dsn`, `Stripe__SecretKey`, `Stripe__WebhookSigningSecret`, `Storage__*`, `Jobs__SchedulerSharedSecret`, optional `Twilio__*`/`SendGrid__*`)
- [ ] T006 [P] Configure GitHub Actions deploy secrets (repo settings): Workload Identity Federation provider + GCP service account, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `DEPLOY_ALERT_WEBHOOK_URL`; document in `specs/009-dev-auto-deploy/quickstart.md`

**Checkpoint**: Dev platform resources and CI secrets exist; pipeline has something to deploy into.

---

## Phase 2: Foundational (Backend Env-Name Refactor — BLOCKS US1)

**Purpose**: Make migrations/seed/Swagger/CORS configuration-driven so the backend runs correctly
as a deployed `ASPNETCORE_ENVIRONMENT=Dev` service (today they are hardcoded to `IsDevelopment()`,
which would skip migrations/seed and block the deployed frontend via localhost-only CORS). See
`research.md` D4.

**⚠️ CRITICAL**: US1 backend deploy cannot succeed until this phase is complete.

- [X] T007 Create `StartupOptions` (ApplyMigrations, SeedData, EnableSwagger) in `HOAManagementCompany/Infrastructure/Configuration/StartupOptions.cs`
- [X] T008 Refactor `HOAManagementCompany/Program.cs` to bind the `Startup` section and gate the startup migrations/seed block and Swagger on the flags instead of `IsDevelopment()` (Production defaults: all false)
- [X] T009 Make **only** the CORS origins config-driven via `Cors:AllowedOrigins` in `HOAManagementCompany/Program.cs` and add the default keys to `HOAManagementCompany/appsettings.json` (Development keeps localhost defaults). **Preserve** the explicit `.WithHeaders(...)`/`.WithMethods(...)` lists added by PR #28 — do NOT reintroduce `AllowAnyHeader()`/`AllowAnyMethod()` (SonarCloud S5122; gate is now blocking)
- [X] T010 Relax the `--seed` CLI guard and startup seed gate to allow the `Dev` env name in `HOAManagementCompany/Program.cs` (and `HOAManagementCompany/Seed/DatabaseSeeder.cs` if it enforces environment)
- [X] T011 [P] Add `HOAManagementCompany/appsettings.Dev.json` with Dev defaults (`Startup:ApplyMigrations=true`, `Startup:SeedData=true`, `Startup:EnableSwagger=true`, `Cors:AllowedOrigins` = Dev Pages origin). **No secret values** in this file — all Dev secrets come from Secret Manager (S2068; SC-006)
- [X] T012 [P] Add xUnit tests asserting flag-gating across env names (Production = no migrate/seed/swagger; Dev = all on; CORS from config) in `HOAManagementCompany.Tests/Startup/StartupConfigTests.cs` — **must cover `StartupOptions.cs`** (it is NOT in `sonar.coverage.exclusions`, unlike `Program.cs`; the 90% diff-coverage gate applies)
- [X] T013 Update the Repowise `domain=bootstrap` marker note in `HOAManagementCompany/Program.cs` to describe config-driven migrations/seed/Swagger/CORS gating

**Checkpoint**: Local `docker-compose up` behavior is unchanged; the backend can run as `Dev` with
production-safe errors, auto-migrate, seed, and config-driven CORS.

---

## Phase 3: User Story 1 - Backend auto-deploys to Dev on merge to main (Priority: P1) 🎯 MVP

**Goal**: Every merge to `main` releases the backend image to the isolated Dev Cloud Run service,
applying migrations + seed at startup, with no manual step.

**Independent Test**: Merge a trivial backend change to `main`; confirm the Dev backend serves the
new behavior at `https://api-dev.nekohoa.com/health` and the migration/seed ran on the Neon Dev DB.

- [X] T014 [US1] Append a `deploy-dev` job to `.github/workflows/test.yml` (`needs: [docker-push]`, `if: github.ref == 'refs/heads/main' && github.event_name == 'push'`, checkout)
- [X] T015 [US1] Add GCP auth via `google-github-actions/auth` (Workload Identity Federation) + `setup-gcloud` to the `deploy-dev` job in `.github/workflows/test.yml`; **pin both actions to a commit SHA** with the version as a trailing comment (SonarCloud S6719, matching the PR #28 precedent)
- [X] T016 [US1] Deploy `sakurapatch/nekohoa-api:${{ github.sha }}` to Cloud Run `nekohoa-api-dev` with `--no-traffic --tag candidate`, `ASPNETCORE_ENVIRONMENT=Dev`, and `--set-secrets` refs (per `contracts/environment-matrix.md`) in `.github/workflows/test.yml`
- [X] T017 [US1] Add the backend **health gate**: poll the candidate tagged URL `…/health` until `Healthy` with a migration-aware timeout in `.github/workflows/test.yml`
- [X] T018 [US1] Promote backend traffic on health pass: `gcloud run services update-traffic nekohoa-api-dev --to-tags candidate=100` in `.github/workflows/test.yml` *(gating is hardened in US3/T025)*

**Checkpoint**: Merging to `main` makes the backend live in Dev with migrations + seed applied —
MVP deliverable.

---

## Phase 4: User Story 2 - Frontend auto-deploys to Dev on merge to main (Priority: P2)

**Goal**: Every merge also publishes the Angular app to Cloudflare Pages Dev, pointed at the Dev API.

**Independent Test**: Merge a visible frontend change; confirm the Dev frontend URL serves the
update and calls `api-dev.nekohoa.com` (not Staging/Prod).

- [X] T019 [P] [US2] Add `neko-hoa/src/environments/environment.dev.ts` (apiBaseUrl/telemetryUrl `https://api-dev.nekohoa.com/...`, `propagateTraceHeaderCorsUrls`, Stripe **test** publishable key)
- [X] T020 [US2] Add a `dev` build configuration in `neko-hoa/angular.json` (fileReplacements `environment.ts` → `environment.dev.ts`, `outputHashing: all`, prod budgets)
- [X] T021 [US2] Add frontend build + deploy steps to `deploy-dev` in `.github/workflows/test.yml`: `npm ci`, `npm run build -- --configuration=dev`, deploy `dist/neko-hoa/browser` to Cloudflare **Pages preview** (pin the Cloudflare Pages/wrangler action to a commit SHA — S6719)
- [X] T022 [US2] Promote the Pages preview to the Dev production alias in `.github/workflows/test.yml` *(gating is hardened in US3/T025)*

**Checkpoint**: Merging to `main` makes the full Dev app (backend + frontend) live end to end.

---

## Phase 5: User Story 3 - Failed deploys never take down Dev (Priority: P3)

**Goal**: A new release is verified by the full E2E suite before it gets Dev traffic; on any
failure the prior healthy release keeps serving and the team is alerted; the latest commit wins.

**Independent Test**: Merge a change that fails E2E (or a broken migration); confirm the job fails,
the previous Dev release still serves at the Dev URLs, no promotion occurred, and a chat alert was
posted.

- [X] T023 [US3] Add Dev E2E configuration targeting the deployed Dev URLs (baseUrl = Dev Pages preview, API = `api-dev`), using the seeded login user and test-mode Stripe seam, in `neko-hoa/cypress.config.ts` and/or `neko-hoa/playwright.config.ts` (parameterized via CI env vars)
- [X] T024 [US3] Add the **E2E gate** step to `deploy-dev` running the full suite against the Dev candidate/preview *before* promotion in `.github/workflows/test.yml`
- [X] T025 [US3] Gate the backend promote (T018) and frontend promote (T022) on **health + E2E success**, so a failure skips promotion and leaves the prior release serving, in `.github/workflows/test.yml`
- [X] T026 [P] [US3] Add `concurrency: { group: deploy-dev, cancel-in-progress: true }` to the `deploy-dev` job so a newer merge wins in `.github/workflows/test.yml`
- [X] T027 [P] [US3] Add an `if: failure()` notification step posting commit + failed step to `DEPLOY_ALERT_WEBHOOK_URL` in `.github/workflows/test.yml`

**Checkpoint**: Deploys are safe — broken merges never replace the working Dev release, and
failures are visible in team chat.

---

## Phase 6: User Story 4 - Dev environment is isolated and self-configuring (Priority: P3)

**Goal**: Prove and harden that Dev uses its own DB/identity/secrets, sourced from a managed store,
never committed or baked into images.

**Independent Test**: Run the `contracts/environment-matrix.md` verification checklist; all
isolation and no-leaked-secret checks pass.

- [ ] T028 [P] [US4] Confirm the `deploy-dev` Cloud Run step passes every secret as a `--set-secrets` ref (no literal values) and review against `contracts/environment-matrix.md` in `.github/workflows/test.yml`
- [ ] T029 [P] [US4] Confirm GCP auth uses Workload Identity Federation (no exportable SA JSON key) and document the rationale in `specs/009-dev-auto-deploy/quickstart.md`
- [ ] T030 [P] [US4] Add a CI assertion (or documented check) that no secret values are baked into the image layers (`docker history` / image inspect) in `.github/workflows/test.yml`
- [ ] T031 [US4] Verify Dev DB and secret set are distinct from Staging/Prod (distinct connection string + distinct secret refs) per the `contracts/environment-matrix.md` checklist
- [ ] T031a [P] [US4] Verify Dev access control (FR-011a): an **unauthenticated** request to a protected Dev endpoint is rejected (401/403), confirming the app's existing login/token scheme gates Dev — record in the `contracts/environment-matrix.md` checklist

**Checkpoint**: Isolation invariant (SC-005) and no-leaked-secrets invariant (SC-006) verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and constitution gates.

- [ ] T032 [P] Run the `specs/009-dev-auto-deploy/quickstart.md` end-to-end verification (merge → watch `deploy-dev` → confirm live); **record the end-to-end wall-clock from merge to live and confirm it is ≤ 30 minutes** incl. the E2E gate (SC-002) — if exceeded, treat the 30-min target as monitored and note the cause
- [ ] T033 [P] Confirm Sentry on the Dev service carries `environment=Dev` + release id and excludes sensitive content
- [ ] T034 [P] Repowise review: regenerate/confirm the `Program.cs` `domain=bootstrap` marker region; indexed docs match merged code
- [ ] T035 Confirm `/swagger` is reachable in Dev and disabled in Production per `contracts/environment-matrix.md`
- [ ] T036 Migration review: confirm any destructive migration carries a rollback/mitigation plan before reaching Dev (FR-017)
- [ ] T037 Confirm the PR scope is a focused vertical slice (CI/CD + config) per the constitution and that **both** coverage gates pass — the constitution's **95% relevant-file** coverage (Codecov project) **and** the **90% diff-coverage** on changed lines — plus the now-**blocking** SonarQube quality gate and Codecov upload (the deploy chain inherits these via `docker-push` ← `test`). If the 95% relevant-file gate does not apply to a config-only change, document why.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — platform provisioning can start immediately.
- **Foundational (Phase 2)**: Independent of Phase 1 code-wise, but **blocks US1**. Can be done in
  parallel with provisioning.
- **US1 (Phase 3)**: Depends on Phase 2 (backend refactor) + Phase 1 (Cloud Run service, secrets).
- **US2 (Phase 4)**: Depends on Phase 1 (Pages project). Independent of US1 code; shares the
  `deploy-dev` job created in T014.
- **US3 (Phase 5)**: Depends on US1 + US2 (it gates and hardens their promote steps).
- **US4 (Phase 6)**: Depends on US1 (the deploy step exists to verify); mostly verification.
- **Polish (Phase 7)**: After all desired stories complete.

### Critical path

T005/T006 (secrets) + T007→T008→T009/T010 (refactor) → T014→T016→T017→T018 (US1 MVP) →
T021/T022 (US2) → T024→T025 (US3 gate) → verification.

### Within stories

- US1: T014 (job scaffold) before T015–T018; T016 before T017 before T018.
- US2: T019 before T020 (config references the env file); T021 before T022.
- US3: T023 before T024; T024 before T025.

---

## Parallel Opportunities

- **Setup**: T002, T003, T004, T006 run in parallel (distinct resources); T001 and T005 gate on the
  resources they describe.
- **Foundational**: T011 and T012 are [P] (distinct files) once T007–T010 land.
- **US3**: T026 and T027 are [P] (distinct, additive workflow blocks).
- **US4**: T028, T029, T030 are [P] (independent checks).
- **Polish**: T032, T033, T034 are [P].

### Parallel example — Foundational

```bash
# After T007–T010 (Program.cs refactor) land:
Task: "Add appsettings.Dev.json (T011)"
Task: "Add StartupConfigTests.cs (T012)"
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 Setup (at least Neon Dev DB, Cloud Run service, secrets, GCP WIF).
2. Phase 2 Foundational (backend refactor) — CRITICAL, blocks US1.
3. Phase 3 US1 — backend auto-deploys to Dev.
4. **STOP and VALIDATE**: merge a backend change → confirm Dev backend live with migrations + seed.

### Incremental Delivery

1. Setup + Foundational → ready.
2. US1 → backend live in Dev (MVP).
3. US2 → full app live in Dev.
4. US3 → deploys are safe (E2E gate, no-downtime-on-failure, chat alerts, latest-wins).
5. US4 → isolation/no-leak verified and hardened.
6. Polish → constitution gates + quickstart validation.

---

## Notes

- The image is **already** built and pushed by the existing `docker-push` job — US1 reuses
  `sakurapatch/nekohoa-api:${{ github.sha }}` and does not rebuild.
- US1/US2 initially promote after health only; **US3/T025 hardens promotion to require the E2E
  gate** — this is intentional incremental delivery, not a contradiction.
- No new database schema or endpoints — do not add migration files for this feature.
- Commit after each task or logical group; keep the PR a focused CI/CD + config vertical slice.

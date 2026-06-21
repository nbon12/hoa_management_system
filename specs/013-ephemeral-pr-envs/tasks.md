---
description: "Task list for 013-ephemeral-pr-envs"
---

# Tasks: Ephemeral per-PR test environments

**Input**: Design documents from `/specs/013-ephemeral-pr-envs/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: This feature is infrastructure + CI. "Tests" here are `tofu validate`/`plan`, Trivy
IaC scans, and the **existing** Cypress/Playwright smoke suites run against the per-PR URLs, plus
the backend integration/storage tests pointed at real R2 + a Neon branch. Each mandatory
acceptance scenario maps to a runnable workflow/suite check (constitution §11).

**Organization**: Grouped by user story (P1 → P2 → P3) so each ships independently. Naming and
state are namespaced by `pr_number` throughout.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (Setup/Foundational/Polish carry no story label)

## Path Conventions

Infra under `infra/`, workflows under `.github/workflows/`, helper scripts under `scripts/`,
frontend under `neko-hoa/`. Application backend is unchanged.

---

## Phase 1: Setup (Shared Scaffolding)

**Purpose**: Create the empty module + root skeletons and validation wiring.

- [X] T001 [P] Create `infra/modules/pr-environment/versions.tf` pinning providers per plan (`hashicorp/google ~5.0`, `hashicorp/google-beta ~5.0`, `cloudflare/cloudflare ~4.0`, `kislerdm/neon =0.6.3`)
- [X] T002 Create `infra/modules/pr-environment/variables.tf` with the input surface from `contracts/pr-environment-module.md` (`pr_number`, `head_sha`, `gcp_project_id`, `gcp_region`, `neon_project_id`, `neon_base_branch`, `neon_api_key`, `cloudflare_account_id`, `cloudflare_api_token`, `runtime_service_account`, `shared_secret_ids`, `stripe_publishable_key`, `labels`)
- [X] T003 Create `infra/modules/pr-environment/outputs.tf` declaring `api_url`, `web_branch`, `neon_branch_id`, `db_connection_secret_id`, `stripe_webhook_secret_id`, `r2_bucket_name`
- [X] T004 [P] Create `infra/environments/pr/backend.tf` (GCS bucket `nekohoa-dev-tfstate`, prefix left to `-backend-config` at init) and `infra/environments/pr/variables.tf` (`pr_number`, `head_sha`, plus pass-through vars)
- [X] T005 Create `infra/environments/pr/main.tf` instantiating `modules/pr-environment`, merging `labels = { "pr-env" = "true", "pr-number" = tostring(var.pr_number) }`
- [X] T006 [P] Verify `tofu -chdir=infra/environments/pr fmt -check` and `tofu validate` pass with placeholder/empty resources (gate is green before resources are added)

**Checkpoint**: Module + root skeleton compiles and validates.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared primitives and ops settings every story depends on. **No story work begins until this is done.**

- [X] T007 Enable `billingbudgets.googleapis.com` (and confirm `cloudresourcemanager`/`serviceusage`) in `infra/bootstrap/state-bucket/main.tf` (or the bootstrap APIs list)
- [X] T008 Wire shared-primitive inputs into `infra/environments/pr/main.tf`: reference the existing Neon project `super-water-18090867`, WIF pool/provider `github-pool-dev`, runtime SA `nekohoa-run-dev`, and the 8 shared operator secret ids (no per-PR recreation)
- [ ] T009 Create the long-lived **`pr-base`** Neon branch (Dev-shaped schema, seeded once) and document/automate its creation; set `neon_base_branch = "pr-base"` default
- [ ] T010 [P] **Ops runbook task** (per `quickstart.md`): set Repo → Settings → Actions → "Require approval for all external contributors"; create a required-reviewer GitHub Environment (`pr-preview`) and move Neon/Cloudflare/GCP/Docker Hub/Stripe-test secrets into it
- [X] T011 [P] Add a reusable composite step or snippet for WIF auth + `tofu init -backend-config="prefix=state/pr/${PR_NUMBER}"` to be shared by provision/teardown/sweep workflows

**Checkpoint**: Shared primitives referenced, `pr-base` exists, fork gate + secret Environment configured.

---

## Phase 3: User Story 1 - PR validated against real storage and database (Priority: P1) 🎯 MVP

**Goal**: Each qualifying PR gets an isolated Neon branch + real R2 bucket; the PR's backend storage/integration tests run against them before merge.

**Independent Test**: Open a PR exercising document upload/download; confirm checks run against `nekohoa-pr-<n>-documents` (real R2) + the `pr-<n>` Neon branch, fail on a real-R2-incompatible change, and pass when compatible — with no shared-env dependency and no effect on other PRs.

### Implementation for User Story 1

- [X] T012 [P] [US1] Create `infra/modules/pr-environment/neon.tf`: `neon_branch` (fork of `var.neon_base_branch`), pooled `neon_endpoint`, `neon_role`, `neon_database`, named `pr-<n>`
- [X] T013 [P] [US1] Create `infra/modules/pr-environment/r2.tf`: `cloudflare_r2_bucket` `nekohoa-pr-<n>-documents`
- [X] T014 [US1] Create `infra/modules/pr-environment/secrets.tf` for `pr-<n>-db-connection` (Neon pooled connection) with `pr-env`/`pr-number` labels; wire module outputs (depends on T012)
- [X] T015 [US1] Create `.github/workflows/pr-env.yml` provisioning skeleton: `on: pull_request` types `[opened, synchronize, reopened, ready_for_review]`, `paths` filter (app/infra paths per `contracts/workflows.md`), guard `head.repo.full_name == github.repository && draft == false`, concurrency `pr-env-${{ github.event.number }}` cancel-in-progress, WIF auth
- [X] T016 [US1] Add `provision` job to `pr-env.yml`: `tofu init -backend-config=prefix=state/pr/<n>` + `tofu apply -auto-approve` provisioning the Neon branch + R2 bucket + db secret
- [X] T017 [US1] Validate against the **real** per-PR R2 bucket + Neon branch. NOTE (implementation): the backend xUnit suite always uses Testcontainers + MinIO (set in the fixture constructor before the app starts), so env-var redirection cannot point it at real services. Real-storage/db **incompatibility detection therefore happens end-to-end** via the deployed PR app's Playwright document/payment flows (US2). `pr-env.yml` adds (a) a real-resource **reachability** assertion (the Neon branch host + an `aws s3 ls` on the per-PR R2 bucket) as US1-layer evidence (SC-001), and (b) the Playwright/Cypress e2e that catches incompatibility (SC-002).
- [X] T018 [US1] Ensure provisioning failure fails the check clearly and leaves no billable resources (FR-009): on `tofu apply` failure, run `tofu destroy` for the prefix in a cleanup step. **At capacity** (quota exhausted), fail fast with a clear message rather than queueing (modest-concurrency assumption; see spec Edge "Concurrent PRs")
- [X] T019 [US1] Verify isolation (SC-006): assert distinct branch/bucket names per PR and no cross-PR access in the test setup

**Checkpoint**: US1 is independently shippable — real DB + storage isolation and incompatibility detection on every qualifying PR (the MVP), with no per-PR app deployment yet.

---

## Phase 4: User Story 2 - PR validated end-to-end against its own running application (Priority: P2)

**Goal**: Each PR gets its own Cloud Run API + Cloudflare Pages preview wired to its DB/storage; smoke tests run against that instance (no promotion step).

**Independent Test**: Open a PR changing a user-facing flow; confirm `pr-<n>.nekohoa-dev.pages.dev` serves the PR's code wired to the PR API, cross-origin works with no manual config, and a regression fails the PR's Playwright checks before merge.

### Implementation for User Story 2

- [X] T020 [P] [US2] Create `infra/modules/pr-environment/cloud_run.tf`: `google_cloud_run_v2_service` `nekohoa-api-pr-<n>` (image `sakurapatch/nekohoa-api:pr-<n>-<sha>`, `ASPNETCORE_ENVIRONMENT=Dev`, scale-to-zero, runtime SA, `pr-env`/`pr-number` labels) + public-invoker IAM (`allUsers`)
- [X] T021 [US2] Add `pr-<n>-stripe-webhook` secret to `infra/modules/pr-environment/secrets.tf`; wire Cloud Run env: db-connection + stripe-webhook (per-PR) + shared operator secrets + `Storage__BucketName` + `Stripe:PublishableKey`. Confirm the per-PR pooled Neon endpoint keeps low max-connections so concurrent PR envs stay within Neon limits (constitution §8)
- [X] T022 [P] [US2] Create `scripts/stripe-webhook-register.sh` per `contracts/stripe-webhook.md`: create a test-mode webhook to `<api_url>/api/v1/payments/webhook` with `metadata[pr]=<n>`, write the signing secret into `pr-<n>-stripe-webhook`
- [X] T023 [P] [US2] Add per-PR API base URL injection to the Angular build in `neko-hoa/` (build-time substitution / env config consumed by the existing boot-time config guard)
- [X] T024 [P] [US2] Add a Jasmine/Karma (or boot-guard) unit test in `neko-hoa/` verifying the injected per-PR API base URL is consumed correctly and the boot-time config guard fails loudly when it is missing (constitution §8/§9 coverage for the T023 app-code change)
- [X] T025 [US2] Extend `pr-env.yml`: `build-image` (push `pr-<n>-<sha>`) before provision; after provision add `stripe-webhook` (T022), `frontend` (`wrangler pages deploy --branch=pr-<n>` on `nekohoa-dev`), and `health` (poll `<api_url>/health` ≤300s) jobs
- [X] T026 [US2] Add `e2e` job to `pr-env.yml`: run Cypress (`CYPRESS_BASE_URL`/`DEV_API_BASE_URL`) then Playwright (`PLAYWRIGHT_BASE_URL`/`PLAYWRIGHT_API_URL`) against the PR URLs (reuse `e2e:dev` / `e2e:playwright-dev`)
- [X] T027 [US2] Add `report` job: upsert a sticky PR comment with the `{api, web}` URLs and status
- [X] T028 [US2] Verify CORS (FR-005): confirm the PR API (running as `Dev`) accepts the `pr-<n>.nekohoa-dev.pages.dev` origin with no manual config (reuses PR #66 preview-origin allowance)

**Checkpoint**: US1 + US2 both work — full per-PR app exercised end-to-end, no promotion deadlock (SC-007).

---

## Phase 5: User Story 3 - Automatic teardown and cost control (Priority: P3)

**Goal**: Environments are destroyed on close/merge, reclaimed on inactivity/orphan, and capped by a checked-in budget.

**Independent Test**: Merge/close a PR and confirm every `pr-env`-labeled resource is gone within 30 min; `workflow_dispatch` the sweep and confirm a closed-PR env and a >7-day-idle env are destroyed; `tofu plan` shows the budget.

### Implementation for User Story 3

- [X] T029 [P] [US3] Create `scripts/stripe-webhook-deregister.sh`: find endpoints by `metadata[pr]==<n>` and delete (idempotent, ignore 404)
- [X] T030 [US3] Create `.github/workflows/pr-env-teardown.yml`: `on: pull_request [closed]`, head-repo guard, concurrency `pr-env-${{ github.event.number }}`; steps `tofu destroy` prefix `state/pr/<n>` → delete state object → deregister Stripe webhook → delete Pages `pr-<n>` branch → delete `pr-<n>-*` image tags (each best-effort/idempotent, SC-004)
- [X] T031 [US3] Create `.github/workflows/pr-env-sweep.yml`: `schedule: cron "13 7 * * *"` + `workflow_dispatch`; enumerate `state/pr/*` prefixes; destroy orphans (PR closed but env present, SC-005) and inactive envs (PR open, last commit > 7 days, FR-007) with a reclaim comment
- [X] T032 [US3] Add `pr-base` self-heal to the sweep: recreate the base Neon branch from Dev if missing (D3)
- [X] T033 [P] [US3] Add `google_billing_budget` to `infra/environments/dev` filtered by label `pr-env=true`, amount via new tfvar `pr_env_monthly_budget` (default 25), threshold rules 0.8 + 1.0, notification channel; add `billing_account_id` input (SC-008)
- [ ] T034 [US3] Verify teardown completeness: after closing a PR, confirm zero resources labeled `pr-env=true,pr-number=<n>` remain across Cloud Run, R2, Neon branches, secrets, and `state/pr/<n>` (FR-006)

**Checkpoint**: All three stories functional — provision, validate, tear down, reclaim, and cap cost.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T035 [P] Confirm Trivy IaC scan (`security-scan.yml`) covers the new HCL under `infra/modules/pr-environment` and `infra/environments/pr`; resolve or `.trivyignore` findings with justification
- [X] T036 [P] Add Repowise marker regions/content for `infra/modules/pr-environment/*.tf` (`domain=pr-environment`) and `.github/workflows/pr-env*.yml` (`section=pr-env-ci`); run the Repowise PR workflow and commit refreshed outputs
- [X] T037 [P] Secret-safety review: confirm per-PR secrets are never echoed to logs or PR comments and are deleted on teardown/reclaim (FR-010)
- [X] T038 Cost sanity check: confirm scale-to-zero on Cloud Run (`min-instances=0`) and Neon branch autosuspend so idle PR envs cost ~pennies/day; confirm CI minutes are free (public repo)
- [X] T039 [P] **Production-data isolation audit** (FR-011 / SC-009): add an automated check confirming no PR env resource references a production project, Neon branch, R2 bucket, or secret — branches fork only from `pr-base`; assert in the provision workflow and/or the sweep
- [X] T040 [P] **Neon concurrency check** (constitution §8): verify that, at the expected number of simultaneous PR envs, per-PR pooled endpoints with low max-connections do not exhaust Neon capacity (document the headroom; tighten pool size if needed)
- [ ] T041 Run `quickstart.md` validation end-to-end (one real PR: provision → URLs → smoke → close → teardown) and confirm SC-003 (≤10 min ready) and SC-004 (≤30 min torn down)
- [X] T042 **Before submitting the PR**: bring this feature's `spec.md` AND `tasks.md` up to date with the work actually performed; update any older `spec.md` that drifted; reconcile cross-spec contradictions (the out-of-scope items belong to a separate spec — keep that boundary explicit)
- [X] T043 Verify the spec stays executable: every mandatory acceptance scenario / FR maps to a runnable workflow/suite check that currently passes (constitution §11); confirm PR scope is a focused vertical slice or justified cross-cutting infra change

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **blocks all stories** (shared primitives, `pr-base`, fork gate, secret Environment).
- **US1 (Phase 3)**: depends on Foundational. **MVP** — independently shippable.
- **US2 (Phase 4)**: depends on Foundational; builds on the same `pr-env.yml` and module (extends, doesn't block US1's value).
- **US3 (Phase 5)**: depends on Foundational; tears down whatever US1/US2 create (teardown works even with only US1 resources).
- **Polish (Phase 6)**: after the desired stories are complete.

### Within Each Story

- Module resource files (`neon.tf`/`r2.tf`/`cloud_run.tf`) before the workflow job that applies them.
- Provision job before the job that tests against the provisioned resources.
- Scripts (Stripe register/deregister) before the workflow steps that call them.
- App-code change (T023) before its test (T024).

### Parallel Opportunities

- **Setup**: T001, T004, T006 are independent of T002/T003/T005 ordering where marked [P].
- **US1**: T012 (neon) and T013 (r2) in parallel; T014 depends on T012.
- **US2**: T020 (cloud_run), T022 (stripe script), T023 (frontend injection), T024 (frontend test) in parallel; workflow tasks T025–T027 serialize on `pr-env.yml`.
- **US3**: T029 (deregister script) and T033 (budget) in parallel with the workflow tasks T030–T032.
- **Polish**: T035, T036, T037, T039, T040 in parallel.

---

## Parallel Example: User Story 1

```text
# Module resource files in parallel:
Task: "Create infra/modules/pr-environment/neon.tf (branch fork + endpoint + role + db)"
Task: "Create infra/modules/pr-environment/r2.tf (per-PR bucket)"
# Then (depends on neon.tf):
Task: "Create infra/modules/pr-environment/secrets.tf (pr-<n>-db-connection) + wire outputs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (critical — blocks everything).
2. Phase 3 US1: per-PR Neon branch + R2 bucket + backend storage/integration tests against them.
3. **STOP and VALIDATE**: open a real PR, confirm real-R2 incompatibility is caught pre-merge with full isolation.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → real DB+storage isolation (MVP, closes the highest-value gap).
3. US2 → per-PR running app + preview (removes the promotion deadlock).
4. US3 → teardown, reclaim, and the $25 budget guardrail (makes it sustainable).

### Notes

- `[P]` = different files, no dependencies. `[Story]` maps the task to US1/US2/US3.
- Provision is reproducible-as-code (FR-016); teardown is authoritative via `tofu destroy`.
- Fork safety is `pull_request` + head-repo guard + the external-contributor approval gate (no allowlist).
- **At-capacity behavior is fail-fast, not queued** (modest-concurrency assumption); revisit if concurrency grows.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.

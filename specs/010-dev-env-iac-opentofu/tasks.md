---
description: "Task list for 010-dev-env-iac-opentofu implementation"
---

# Tasks: Infrastructure as Code — Declarative Dev Environment Provisioning

**Input**: Design documents from `/specs/010-dev-env-iac-opentofu/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: This feature ships **no application code**, so the constitution's xUnit/Jasmine suites and
95% coverage gate do not apply (see spec → Constitution Requirements). The IaC equivalent —
`tofu fmt -check`, `tofu validate`, a no-diff `tofu plan`, and **name-by-name conformance** against
`contracts/matrix-conformance.md` — is treated as the test layer and is written/run per story.

**Organization**: Tasks are grouped by user story (from spec.md) so each is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US4 maps to the spec's user stories
- All paths are repo-root-relative and absolute under `/home/user/hoa_management_system/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the `infra/` tree and the pinned provider baseline.

- [X] T001 Create the `infra/` directory tree per plan.md (bootstrap/state-bucket/, modules/environment/, environments/dev/)
- [X] T002 [P] Add `infra/.gitignore` excluding `*.tfvars` (except `*.tfvars.example`), `*.tfstate*`, `*.tfplan`, and `.terraform/` (FR-022)
- [X] T003 [P] Author `infra/modules/environment/versions.tf` with `required_version >= 1.8` and **pinned** providers `hashicorp/google`, `hashicorp/google-beta`, `cloudflare/cloudflare`, and the **community** `kislerdm/neon` (exact pin + comment flagging it as community-maintained) (FR-021)
- [X] T004 [P] Create `infra/README.md` skeleton with a Repowise marker region (`section=infra-overview`) describing what `infra/` provisions and its link to the 009 pipeline

**Checkpoint**: `infra/` skeleton exists; `tofu fmt` runs clean on empty configs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: State backend, backend wiring, and the module input surface — required before any
resource file or apply can exist.

**⚠️ CRITICAL**: No user story apply can run until this phase is complete.

- [X] T005 Author `infra/bootstrap/state-bucket/main.tf` + `variables.tf`: a **versioned** `google_storage_bucket` (uniform bucket-level access) using **local state**, plus `google_project_service` to enable `run`, `secretmanager`, `iam`, `iamcredentials`, `sts`, `storage` APIs (FR-020, FR-031)
- [X] T006 [P] Write `infra/bootstrap/state-bucket/README.md` documenting the one-time `tofu init && tofu apply` bootstrap and recording the bucket name for backends
- [X] T007 [P] Author `infra/environments/dev/backend.tf` with `backend "gcs"` (`prefix = "state/dev"`, bucket placeholder filled from bootstrap) (FR-020)
- [X] T008 Author `infra/modules/environment/variables.tf` with the full input surface from data-model.md (env_name, **aspnet_environment**, **secret_prefix**, gcp_project_id, gcp_region, github_repository, frontend_domain, api_domain, api_dns_proxied, container_image, neon_region_id, neon_api_key, cloudflare_*; operator_secrets map; deploy_alert_webhook_url) marking secret-bearing vars `sensitive = true`; document that `aspnet_environment` maps `dev→Dev`/`staging→Staging`/`prod→Production` (NOT `title(env_name)`) (FR-015, FR-030)
- [X] T009 [P] Create `infra/environments/dev/terraform.tfvars.example` — a non-secret, placeholder-only template of every variable (FR-022)
- [X] T010 Author `infra/environments/dev/variables.tf` + `main.tf` instantiating `module "environment"` with `env_name = "dev"`, `aspnet_environment = "Dev"`, `secret_prefix = "dev"`, and Dev domains/region wired through (FR-030)
- [X] T011 [P] Configure provider blocks (`google`, `google-beta`, `cloudflare`, `neon`) in the dev env reading credentials from variables/`TF_VAR_*` (no static keys) (FR-027)

**Checkpoint**: `tofu init`/`validate` succeed for `environments/dev` against the bootstrapped backend; module input contract is fixed.

---

## Phase 3: User Story 1 - Stand up the entire Dev environment with one apply (Priority: P1) 🎯 MVP

**Goal**: A single plan→apply creates every resource the 009 pipeline depends on, matching the
environment-matrix names/values, with `dev-db-connection` auto-wired from Neon.

**Independent Test**: From a clean account, `tofu apply` `environments/dev`, then verify every row of
`contracts/matrix-conformance.md` (1–20) exists with the contracted value and `dev-db-connection` is
the .NET keyword string; re-plan shows zero drift.

### Resource definitions for User Story 1

- [X] T012 [P] [US1] Author `infra/modules/environment/neon.tf`: `neon_project`, `neon_branch` (name `dev`), `neon_database`, `neon_role`; assemble the **pooled** .NET keyword connection string (`Host=…-pooler;…;SSL Mode=Require;Channel Binding=Require`) as a `local` (FR-001/002/003)
- [X] T013 [P] [US1] Author `infra/modules/environment/cloud_run.tf`: `google_cloud_run_v2_service` named `nekohoa-api-${env_name}` (Dev → `nekohoa-api-dev`), `min_instance_count = 0`, container port `8080`, env `ASPNETCORE_ENVIRONMENT = var.aspnet_environment` (Dev → `Dev`), startup+liveness probe on `/health`, runtime SA, secret env refs for the 9 secrets → their .NET keys, and `lifecycle { ignore_changes = [image, client, client_version] }` (FR-005/006/007/008)
- [X] T014 [US1] Add `google_cloud_run_v2_service_iam_member` granting `roles/run.invoker` to `allUsers` (= allow-unauthenticated) in `cloud_run.tf` (FR-006)
- [X] T015 [US1] Add `google_cloud_run_domain_mapping` for `api-dev.nekohoa.com` in `cloud_run.tf` (FR-018)
- [X] T016 [P] [US1] Author `infra/modules/environment/iam.tf`: runtime SA + deployer SA; bind deployer `roles/run.admin` (project) and `roles/iam.serviceAccountUser` on the runtime SA (FR-009/010)
- [X] T017 [US1] In `iam.tf` add the WIF pool + OIDC provider (issuer `token.actions.githubusercontent.com`, attribute map, **attribute condition** `assertion.repository == var.github_repository`) and a `workloadIdentityUser` binding for the deployer SA via `principalSet…/attribute.repository/<repo>` (FR-011/012)
- [X] T018 [P] [US1] Author `infra/modules/environment/secrets.tf`: the nine `google_secret_manager_secret` with IDs `"${var.secret_prefix}-…"` (Dev resolves to the exact `dev-*` IDs); `${secret_prefix}-db-connection` version = the Neon keyword local; the eight operator secrets' versions from `var.operator_secrets` with `lifecycle { ignore_changes = [secret_data] }`; per-secret `secretAccessor` IAM member for the runtime SA (FR-013/014/015)
- [X] T019 [P] [US1] Author `infra/modules/environment/cloudflare.tf`: `cloudflare_pages_project` `nekohoa-${env_name}` (`production_branch = "main"`), `cloudflare_pages_domain` for `dev.nekohoa.com`, `cloudflare_r2_bucket` for Dev documents, `cloudflare_record` for `dev.nekohoa.com` (CNAME→Pages, proxied) and `api-dev.nekohoa.com` (CNAME→`ghs.googlehosted.com`, `proxied = var.api_dns_proxied`) (FR-016/017/018/019)

### Validation for User Story 1

- [X] T020 [US1] Run `tofu fmt -check` and `tofu validate` on the module + dev env; fix until clean
- [X] T021 [US1] Run `tofu plan` for `environments/dev` (grey-cloud: `api_dns_proxied = false`); confirm planned names/values match `contracts/matrix-conformance.md` rows 1–20 — including that the resolved secret IDs equal the literal `dev-*` (with `secret_prefix = "dev"`) and `ASPNETCORE_ENVIRONMENT = Dev` (FR-029)
- [ ] T022 [US1] `tofu apply` step 1 (grey-cloud), wait for the Cloud Run domain-mapping cert, set `api_dns_proxied = true`, `tofu apply` step 2 (proxied Full(strict)) per quickstart.md Steps 3–4 (FR-019)
- [ ] T023 [US1] Verify acceptance: `gcloud run services describe nekohoa-api-dev` shows rows 1–6 + secret refs; `dev-db-connection` is keyword format (no `postgresql://`); the Dev Neon project/branch is distinct from any Staging/Prod (FR-004); **re-`plan` reports zero drift** (SC-002/003/004)

**Checkpoint**: Dev environment is fully provisioned and matches the 009 contract — MVP complete.

---

## Phase 4: User Story 2 - Discover GitHub Actions wiring from outputs (Priority: P2)

**Goal**: `tofu output` prints every GitHub secret/variable the 009 job needs, with sensitive values
masked and the "enable last" instruction.

**Independent Test**: After apply, `tofu output` yields all values in
`contracts/github-actions-wiring.md`; sensitive ones show `<sensitive>`; `next_steps` ends with set
`DEV_DEPLOY_ENABLED=true` last.

- [X] T024 [P] [US2] Author `infra/modules/environment/outputs.tf`: `db_connection_string` (`sensitive`), `wif_provider`, `deployer_service_account`, `gcp_region`, `cloudflare_account_id`, `cloudflare_api_token` (`sensitive`), `deploy_alert_webhook_url` (`sensitive`), `cloud_run_service_url`, and `next_steps` (FR-023/024)
- [X] T025 [P] [US2] Compose the `next_steps` output string: list the five secrets + `GCP_REGION`, then explicitly instruct setting `DEV_DEPLOY_ENABLED=true` **last** (FR-023)
- [X] T026 [US2] Re-export all module outputs from `infra/environments/dev/outputs.tf` unchanged
- [ ] T027 [US2] Verify acceptance: `tofu output` covers every row of `contracts/github-actions-wiring.md`; sensitive outputs are masked in the `apply` summary (SC-005/006)

**Checkpoint**: An operator can wire GitHub Actions from outputs alone.

---

## Phase 5: User Story 3 - Plan-on-PR and gated apply-on-merge (Priority: P2)

**Goal**: PRs touching `infra/` get a plan-only preview; merges auto-apply Dev (and Staging), with
Prod gated behind a protected GitHub Environment.

**Independent Test**: A PR editing `infra/` runs plan-only (no live change); a merge runs apply; both
authenticate via WIF with no committed static keys.

- [X] T028 [P] [US3] Author `.github/workflows/infra-plan.yml`: `pull_request` + `paths: [infra/**]`; `google-github-actions/auth` via WIF (`GCP_WIF_PROVIDER`/`GCP_DEPLOY_SERVICE_ACCOUNT`); `tofu init/fmt -check/validate/plan` for `environments/dev`; post plan summary to the PR; CF/Neon creds via `TF_VAR_*` GitHub secrets (FR-025/027)
- [X] T029 [P] [US3] Author `.github/workflows/infra-apply.yml`: `push` to `main` + `paths: [infra/**]`; a **Dev** apply job that runs automatically; a placeholder/commented **Prod** apply job targeting a protected GitHub Environment `prod` with a required reviewer (auto-apply Dev/Staging, gate Prod) (FR-026)
- [X] T030 [US3] Document/create the GitHub Environment `prod` (required reviewer) and confirm `infra-plan.yml` makes no live changes while `infra-apply.yml` is path-filtered to `infra/**` (SC-007)

**Checkpoint**: Infra changes flow through reviewed plan → gated apply.

---

## Phase 6: User Story 4 - Extend cleanly to Staging/Prod (Priority: P3)

**Goal**: Adding an environment needs only new inputs + isolated state, with zero edits to the shared
module.

**Independent Test**: A reviewer confirms all env-specific values are module inputs; a Staging
skeleton instantiates the module with new tfvars + backend prefix and `tofu validate`s without
touching `modules/environment`.

- [X] T031 [US4] Audit `modules/environment` for any hardcoded `dev`/region/domain; replace with `var.env_name`-derived values, `var.secret_prefix` for the `dev-*` IDs, and `var.aspnet_environment` for the runtime env (confirm no `title(env_name)` shortcut — prod must yield `Production`, not `Prod`) per `contracts/module-interface.md` (FR-030)
- [X] T032 [P] [US4] Add an `infra/environments/staging/` skeleton (backend `prefix = "state/staging"`, `terraform.tfvars.example`, `main.tf` module block) as proof of reuse — not applied (SC-009)
- [X] T033 [US4] Verify acceptance: `tofu validate` the staging skeleton; confirm no change was needed in `modules/environment` (SC-009)

**Checkpoint**: Reuse to Staging/Prod is demonstrated structurally.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T034 [P] Fill the `infra/README.md` Repowise region (`section=infra-overview`) and add `section=env-module-outputs` to `modules/environment/outputs.tf`; run the Repowise marker validation (constitution §9)
- [X] T035 [P] Final `tofu fmt -check` across `infra/`; ensure no secret value appears in any committed file (`git grep` for tfvars values / tokens) (SC-005)
- [ ] T036 Run the full `quickstart.md` validation checklist end-to-end (SC-001–SC-008)
- [X] T037 Confirm PR scope is the focused cross-cutting infra slice and the 009 `deploy-dev` job's referenced secret/variable names still match `contracts/github-actions-wiring.md` (FR-029, §11)
- [X] T038 [P] Confirm the Sonar / Codecov / coverage CI gates **exclude `infra/**`** (e.g. `sonar-project.properties` exclusions + Codecov `ignore`/flags), or document the PR as a justified cross-cutting change under §11, so the HCL-only implement PR is not blocked by a 0%-coverage check (constitution §9/§11)

### Companion config-validation guard (constitution §8 — fail-fast config)

> The IaC sets `ASPNETCORE_ENVIRONMENT` (FR-006/FR-030); these tasks add the matching app-side guard
> so a mis-set value (e.g. `prod` instead of `Production`) is rejected at boot, per the constitution's
> "all configuration MUST be validated at startup" rule. Small companion change to the app (008 style);
> if delivered separately to keep this PR infra-only, track it as a linked follow-up.

- [X] T039 [P] Add a **FluentValidation** startup validator asserting `ASPNETCORE_ENVIRONMENT` (host environment name) is in the known set — `Development` (local), `Dev` (deployed dev), `Test`, `Staging`, `Production` (matching the existing `appsettings.{Development,Dev,Test}.json` + Staging/Prod) — so the backend **fails fast** on a mis-set value (e.g. `prod`, or deployed-`Dev` vs local-`Development` confusion), following the existing pattern in `HOAManagementCompany/Infrastructure/Configuration/*OptionsValidator.cs` + `OptionsValidationExtensions.cs`; add an xUnit test proving startup fails on an invalid value (constitution §8)
- [X] T040 [P] Confirm the Angular **boot-time config guard** (008 style) fails loudly when its required environment configuration (e.g. `apiBaseUrl` for the Dev origin) is missing/invalid; extend it if the Dev environment introduces any newly-required value (constitution §8)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup; **blocks** all user stories (state bucket + module input surface + backend).
- **US1 (Phase 3)**: Depends on Foundational. The MVP.
- **US2 (Phase 4)**: Depends on US1 resources existing (outputs reference resource attributes).
- **US3 (Phase 5)**: Depends on US1 (WIF provider/SA must exist to authenticate) + US2 (outputs supply the GH secrets). Workflow files themselves are independent.
- **US4 (Phase 6)**: Depends on US1 module being complete (audits it for parameterization).
- **Polish (Phase 7)**: After all targeted stories.

### Within Each User Story

- IaC validation order: write resources → `fmt`/`validate` → `plan` (matrix check) → `apply` → re-plan (no drift).
- US1 resource files (T012, T013, T016, T018, T019) are in **different files** → parallelizable; T014/T015 edit `cloud_run.tf` (after T013); T017 edits `iam.tf` (after T016).

### Parallel Opportunities

- Setup: T002, T003, T004 in parallel.
- Foundational: T006, T007, T009, T011 in parallel (after their owning files exist).
- US1: T012, T013, T016, T018, T019 in parallel (separate `.tf` files).
- US3: T028 and T029 in parallel (separate workflow files).

---

## Parallel Example: User Story 1

```bash
# Author the independent resource files together (different files, no ordering between them):
Task: "neon.tf — Neon project/branch/db/role + pooled keyword conn string"
Task: "cloud_run.tf — nekohoa-api-dev service (port 8080, Dev, scale-to-zero, /health, ignore image)"
Task: "iam.tf — runtime + deployer SAs and role bindings"
Task: "secrets.tf — 9 Secret Manager entries, db-connection auto-wired"
Task: "cloudflare.tf — Pages nekohoa-dev, R2 bucket, DNS records"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (state bucket + backend + module inputs) → 3. Phase 3 US1
   → **STOP & VALIDATE**: matrix conformance + zero-drift re-plan. The Dev environment now exists and
   the 009 pipeline could be enabled.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → environment provisioned (MVP).
3. US2 → wiring outputs → operator connects GitHub.
4. US3 → plan/apply automation.
5. US4 → reuse proof for Staging/Prod.

---

## Notes

- [P] = different files, no incomplete-task dependencies.
- This feature provisions infra; the only "tests" are `fmt`/`validate`/`plan`, zero-drift re-plan, and
  matrix conformance — there is no application code to unit-test.
- Never commit `*.tfvars` (except `*.example`) or `*.tfstate`; sensitive outputs are `sensitive`.
- The Cloud Run image is pipeline-owned; never let an infra apply revert it (`ignore_changes`).
- Commit after each task or logical group; keep the PR a focused cross-cutting infra slice.

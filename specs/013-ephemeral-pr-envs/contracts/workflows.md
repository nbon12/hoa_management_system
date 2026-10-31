# Contract: GitHub Actions workflows

Three new workflows, plus the manual `pr-env-repair.yml` added for issue #234. All gate on GitHub-native trust; none use `pull_request_target`.

## `pr-env.yml` — provision + validate

| Aspect | Value |
|--------|-------|
| Trigger | `pull_request` types `[opened, synchronize, reopened, ready_for_review]` |
| Paths | `HOAManagementCompany/**`, `neko-hoa/**`, `infra/modules/pr-environment/**`, `infra/environments/pr/**`, `.github/workflows/pr-env.yml`, `.github/actions/pr-env-tofu-init/**` |
| Guard | `github.event.pull_request.head.repo.full_name == github.repository && github.event.pull_request.draft == false` |
| Concurrency | `group: pr-env-${{ github.event.number }}`, `cancel-in-progress: false` (was `true` until issue #234: a cancelled `tofu apply` strands its state lock and leaves untracked resources; GitHub still keeps only the newest pending run) |
| Auth | WIF (`google-github-actions/auth@v3`, SHA-pinned in `.github/actions/pr-env-tofu-init`) as deployer SA; infra secrets are repo-level, with no GitHub Environment gate (amended by PR #70, "drop pr-preview GitHub Environment"; fork safety comes from `pull_request` + the head-repo guard + the external-contributor approval setting, see spec Assumptions › Fork policy) |

**Steps of the single `pr-env` job (ordered gates; first failure stops and fails the PR check, FR-009):**
1. `build-image` → `docker build` + push `<region>-docker.pkg.dev/<project>/images-dev/nekohoa-api:pr-<n>-<sha>` (Artifact Registry; amended 2026-10-28 by PR #242, was Docker Hub `sakurapatch/nekohoa-api`)
2. `provision` → clear a stale state lock (`scripts/pr-env-clear-stale-lock.sh`: only a lock ≥ 45 min old,
   conditional on its generation; before init, which needs the lock when no state exists yet) →
   `tofu init -lock-timeout=5m -backend-config=prefix=state/pr/<n>` → reap
   untracked leftovers of a killed apply (`scripts/pr-env-reap.sh` with `ONLY_UNTRACKED=true`) →
   `tofu apply -auto-approve -lock-timeout=5m` (D1/D2; self-healing added by issue #236)
3. `audit` → the PR's Neon branch must be a child of `pr-base` (no production data, FR-011/SC-009)
4. `stripe-webhook` → create test webhook → write `pr-<n>-stripe-webhook` secret (D9)
5. `deploy` → `gcloud run deploy` the PR image, image only (tofu creates the service with a placeholder image)
6. `health` → poll `<api_url>/health` (≤300s) for migrations+seed
7. `frontend` → Angular build with PR API base URL → `wrangler pages deploy --branch=pr-<n>` (D5)
8. `reachability` → per-PR Neon host + `aws s3 ls` of the per-PR R2 bucket (US1 infra evidence)
9. `e2e` → Cypress (`CYPRESS_BASE_URL`/`DEV_API_BASE_URL`) then Playwright (`PLAYWRIGHT_BASE_URL`/`PLAYWRIGHT_API_URL`)
10. `report` → upsert a sticky PR comment with `{api, web}` URLs + status

**Postconditions**: on success the PR shows a passing check + env URLs; on any failure the
check fails clearly and no billable resources are left (provision is rolled back / swept). The rollback
deregisters the Stripe test webhook, which no state tracks, before `tofu destroy -lock-timeout=5m`.

## `pr-env-teardown.yml` — destroy on close/merge

| Aspect | Value |
|--------|-------|
| Trigger | `pull_request` types `[closed]` (merge or close) |
| Guard | same head-repo guard |
| Concurrency | `group: pr-env-${{ github.event.number }}` (serialize vs provision) |

**Steps (idempotent, best-effort each, FR-006, SC-004 ≤30 min):**
clear a stale state lock (before `tofu init`) → delete Stripe webhook → empty the R2 bucket →
`tofu destroy -lock-timeout=5m`
prefix `state/pr/<n>` → delete the state objects **only if the destroy succeeded** → delete every Pages
`pr-<n>` deployment (all pages). A failure alerts `DEPLOY_ALERT_WEBHOOK_URL`; the kept state is retried by
the sweep (issue #236: deleting state after a failed destroy orphaned PR 217's resources). Teardown does not prune
per-PR image tags. The `images-dev` Artifact Registry cleanup policy deletes `pr-` tags after 14 days (PR #242).

## `pr-env-sweep.yml` — reclaim orphans + inactive (FR-007/008)

| Aspect | Value |
|--------|-------|
| Trigger | `schedule: cron "13 7 * * *"` + `workflow_dispatch` |

**Logic** — enumerate `state/pr/*` prefixes (an empty listing is a clean exit); per PR number:
- PR closed/merged but env present → `tofu destroy` (orphan; SC-005).
- PR open, last commit > 7 days ago → `tofu destroy` + comment "reclaimed; rebuilds on next push" (FR-007).
- Before each destroy, clear a stale state lock (`CHECK_LIVE_RUNS=true`: kept while a `pr-env`,
  teardown or repair run for that PR is unfinished, because the sweep is outside the `pr-env-<n>` group;
  the three workflows end their `run-name` in `(pr-<n>)` so a run can be matched to its PR, and an
  untagged unfinished run counts for every PR). The state is
  deleted only after a successful destroy; a failed one fails the run and is retried next time.
- Verify the `pr-base` Neon branch (`NEON_PR_BASE_BRANCH_ID`) exists; if it is missing, log an `::error::` annotation (the step does not fail the run). Recreating and re-seeding `pr-base` is a manual operator step (`scripts/create-pr-base.sh`, then seed it and update the secret): the sweep alerts rather than self-heals because seeding is app-driven (implemented this way since PR #70).
- Audit by name (`scripts/pr-env-audit.sh`, tested by `scripts/tests/pr-env-ops.test.sh`): every
  `nekohoa-api-pr-<n>` service, `pr-<n>-*` secret, Neon `pr-<n>` branch and `nekohoa-pr-<n>-documents`
  bucket whose PR has no `state/pr/<n>` prefix. A closed PR's fail the run (reap with `pr-env-repair`
  `action=reap`); an open PR's are a warning, since its next `pr-env` run reaps them.

**Guarantee**: after a sweep, zero resources labeled `pr-env=true` belong to a closed PR, or the run
fails and names them.

## `pr-env-repair.yml` — manual repair of one PR's environment (issue #234)

| Aspect | Value |
|--------|-------|
| Trigger | `workflow_dispatch` on `main` only: `pr_number`, `action` (`diagnose`, `unlock`, `destroy`, `reap`, `verify`), `lock_id`, `dry_run` (default `true`), `allow_open_pr` (default `false`) |
| Jobs | `diagnose` always: read-only, WIF as `nekohoa-agent-ro-dev`, read-only provider tokens, no approval. `repair` for a real change or a destroy dry run: `environment: ops` (operator approval), the deployer SA and CI's tokens |
| Concurrency | `repair` only: `group: pr-env-<n>`, `cancel-in-progress: false` (serialises with provision and teardown) |

**Actions**: `diagnose` lists the state objects and every per-PR resource by exact name
(`scripts/pr-env-reap.sh` with `DRY_RUN=true`), never running `tofu`. `unlock` removes
`state/pr/<n>/default.tflock` only if `lock_id` is its generation and it is ≥ 45 min old. `destroy` plans and
applies a destroy from the existing state and deletes the state only if it succeeded. `reap` deletes the
leftovers no state tracks, by exact name and label (`scripts/pr-env-reap.sh`, tested by
`scripts/tests/pr-env-reap.test.sh`). `destroy` and `reap` refuse an open PR unless `allow_open_pr`. `verify` (added by PR #246) runs `scripts/pr-env-verify-ro.sh` (tested by `scripts/tests/pr-env-verify-ro.test.sh`) in the `diagnose` job only, with no approval. Every read by the read-only identities must work, and every write or secret read must be refused. `pr_number` is ignored (pass 0).
Design and runbook: `docs/handoffs/2026-10-26-agent-cloud-access.md`.

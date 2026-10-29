# Contract: GitHub Actions workflows

Three new workflows, plus the manual `pr-env-repair.yml` added for issue #234. All gate on GitHub-native trust; none use `pull_request_target`.

## `pr-env.yml` — provision + validate

| Aspect | Value |
|--------|-------|
| Trigger | `pull_request` types `[opened, synchronize, reopened, ready_for_review]` |
| Paths | `HOAManagementCompany/**`, `neko-hoa/**`, `infra/modules/pr-environment/**`, `infra/environments/pr/**`, `.github/workflows/pr-env.yml` |
| Guard | `github.event.pull_request.head.repo.full_name == github.repository && github.event.pull_request.draft == false` |
| Concurrency | `group: pr-env-${{ github.event.number }}`, `cancel-in-progress: false` (was `true` until issue #234: a cancelled `tofu apply` strands its state lock and leaves untracked resources; GitHub still keeps only the newest pending run) |
| Auth | WIF (`google-github-actions/auth@v3`, SHA-pinned in `.github/actions/pr-env-tofu-init`) as deployer SA; infra secrets via required-reviewer Environment |

**Jobs (ordered gates; first failure stops and fails the PR check, FR-009):**
1. `build-image` → `docker build` + push `sakurapatch/nekohoa-api:pr-<n>-<sha>`
2. `provision` → `tofu init -backend-config=prefix=state/pr/<n>` → clear a stale state lock
   (`scripts/pr-env-clear-stale-lock.sh`: only a lock ≥ 45 min old, conditional on its generation) → reap
   untracked leftovers of a killed apply (`scripts/pr-env-reap.sh` with `ONLY_UNTRACKED=true`) →
   `tofu apply -auto-approve -lock-timeout=5m` (D1/D2; self-healing added by issue #236)
3. `stripe-webhook` → create test webhook → write `pr-<n>-stripe-webhook` secret (D9)
4. `frontend` → Angular build with PR API base URL → `wrangler pages deploy --branch=pr-<n>` (D5)
5. `health` → poll `<api_url>/health` (≤300s) for migrations+seed
6. `e2e` → Cypress (`CYPRESS_BASE_URL`/`DEV_API_BASE_URL`) then Playwright (`PLAYWRIGHT_BASE_URL`/`PLAYWRIGHT_API_URL`)
7. `report` → upsert a sticky PR comment with `{api, web}` URLs + status

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
delete Stripe webhook → empty the R2 bucket → clear a stale state lock → `tofu destroy -lock-timeout=5m`
prefix `state/pr/<n>` → delete the state objects **only if the destroy succeeded** → delete every Pages
`pr-<n>` deployment (all pages). A failure alerts `DEPLOY_ALERT_WEBHOOK_URL`; the kept state is retried by
the sweep (issue #236: deleting state after a failed destroy orphaned PR 217's resources). Per-PR image
tags are not pruned.

## `pr-env-sweep.yml` — reclaim orphans + inactive (FR-007/008)

| Aspect | Value |
|--------|-------|
| Trigger | `schedule: cron "13 7 * * *"` + `workflow_dispatch` |

**Logic** — enumerate `state/pr/*` prefixes (an empty listing is a clean exit); per PR number:
- PR closed/merged but env present → `tofu destroy` (orphan; SC-005).
- PR open, last commit > 7 days ago → `tofu destroy` + comment "reclaimed; rebuilds on next push" (FR-007).
- Before each destroy, clear a stale state lock (`CHECK_LIVE_RUNS=true`: kept while any `pr-env`,
  teardown or repair run is unfinished, because the sweep is outside the `pr-env-<n>` group). The state is
  deleted only after a successful destroy; a failed one fails the run and is retried next time.
- Ensure `pr-base` Neon branch exists; recreate from Dev if missing (self-healing, D3).
- Audit by name (`scripts/pr-env-audit.sh`, tested by `scripts/tests/pr-env-ops.test.sh`): every
  `nekohoa-api-pr-<n>` service, `pr-<n>-*` secret, Neon `pr-<n>` branch and `nekohoa-pr-<n>-documents`
  bucket whose PR has no `state/pr/<n>` prefix. A closed PR's fail the run (reap with `pr-env-repair`
  `action=reap`); an open PR's are a warning, since its next `pr-env` run reaps them.

**Guarantee**: after a sweep, zero resources labeled `pr-env=true` belong to a closed PR, or the run
fails and names them.

## `pr-env-repair.yml` — manual repair of one PR's environment (issue #234)

| Aspect | Value |
|--------|-------|
| Trigger | `workflow_dispatch` on `main` only: `pr_number`, `action` (`diagnose`, `unlock`, `destroy`, `reap`), `lock_id`, `dry_run` (default `true`), `allow_open_pr` (default `false`) |
| Jobs | `diagnose` always: read-only, WIF as `nekohoa-agent-ro-dev`, read-only provider tokens, no approval. `repair` for a real change or a destroy dry run: `environment: ops` (operator approval), the deployer SA and CI's tokens |
| Concurrency | `repair` only: `group: pr-env-<n>`, `cancel-in-progress: false` (serialises with provision and teardown) |

**Actions**: `diagnose` lists the state objects and every per-PR resource by exact name
(`scripts/pr-env-reap.sh` with `DRY_RUN=true`), never running `tofu`. `unlock` removes
`state/pr/<n>/default.tflock` only if `lock_id` is its generation and it is ≥ 45 min old. `destroy` plans and
applies a destroy from the existing state and deletes the state only if it succeeded. `reap` deletes the
leftovers no state tracks, by exact name and label (`scripts/pr-env-reap.sh`, tested by
`scripts/tests/pr-env-reap.test.sh`). `destroy` and `reap` refuse an open PR unless `allow_open_pr`.
Design and runbook: `docs/handoffs/2026-10-26-agent-cloud-access.md`.

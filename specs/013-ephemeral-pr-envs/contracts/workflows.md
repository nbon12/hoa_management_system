# Contract: GitHub Actions workflows

Three new workflows, plus the manual `pr-env-repair.yml` added for issue #234. All gate on GitHub-native trust; none use `pull_request_target`.

## `pr-env.yml` — provision + validate

| Aspect | Value |
|--------|-------|
| Trigger | `pull_request` types `[opened, synchronize, reopened, ready_for_review]` |
| Paths | `HOAManagementCompany/**`, `neko-hoa/**`, `infra/modules/pr-environment/**`, `infra/environments/pr/**`, `.github/workflows/pr-env.yml` |
| Guard | `github.event.pull_request.head.repo.full_name == github.repository && github.event.pull_request.draft == false` |
| Concurrency | `group: pr-env-${{ github.event.number }}`, `cancel-in-progress: false` (was `true` until issue #234: a cancelled `tofu apply` strands its state lock and leaves untracked resources; GitHub still keeps only the newest pending run) |
| Auth | WIF (`google-github-actions/auth@v2`) as deployer SA; infra secrets via required-reviewer Environment |

**Jobs (ordered gates; first failure stops and fails the PR check, FR-009):**
1. `build-image` → `docker build` + push `sakurapatch/nekohoa-api:pr-<n>-<sha>`
2. `provision` → `tofu init -backend-config=prefix=state/pr/<n>` → `tofu apply -auto-approve` (D1/D2)
3. `stripe-webhook` → create test webhook → write `pr-<n>-stripe-webhook` secret (D9)
4. `frontend` → Angular build with PR API base URL → `wrangler pages deploy --branch=pr-<n>` (D5)
5. `health` → poll `<api_url>/health` (≤300s) for migrations+seed
6. `e2e` → Cypress (`CYPRESS_BASE_URL`/`DEV_API_BASE_URL`) then Playwright (`PLAYWRIGHT_BASE_URL`/`PLAYWRIGHT_API_URL`)
7. `report` → upsert a sticky PR comment with `{api, web}` URLs + status

**Postconditions**: on success the PR shows a passing check + env URLs; on any failure the
check fails clearly and no billable resources are left (provision is rolled back / swept).

## `pr-env-teardown.yml` — destroy on close/merge

| Aspect | Value |
|--------|-------|
| Trigger | `pull_request` types `[closed]` (merge or close) |
| Guard | same head-repo guard |
| Concurrency | `group: pr-env-${{ github.event.number }}` (serialize vs provision) |

**Steps (idempotent, best-effort each, FR-006, SC-004 ≤30 min):**
`tofu destroy` prefix `state/pr/<n>` → delete state object → delete Stripe webhook →
delete Pages `pr-<n>` branch → delete `pr-<n>-*` image tags.

## `pr-env-sweep.yml` — reclaim orphans + inactive (FR-007/008)

| Aspect | Value |
|--------|-------|
| Trigger | `schedule: cron "13 7 * * *"` + `workflow_dispatch` |

**Logic** — enumerate `state/pr/*` prefixes; per PR number:
- PR closed/merged but env present → `tofu destroy` (orphan; SC-005).
- PR open, last commit > 7 days ago → `tofu destroy` + comment "reclaimed; rebuilds on next push" (FR-007).
- Ensure `pr-base` Neon branch exists; recreate from Dev if missing (self-healing, D3).

**Guarantee**: after a sweep, zero resources labeled `pr-env=true` belong to a closed PR.

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

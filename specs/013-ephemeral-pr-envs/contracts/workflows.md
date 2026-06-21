# Contract: GitHub Actions workflows

Three new workflows. All gate on GitHub-native trust; none use `pull_request_target`.

## `pr-env.yml` — provision + validate

| Aspect | Value |
|--------|-------|
| Trigger | `pull_request` types `[opened, synchronize, reopened, ready_for_review]` |
| Paths | `HOAManagementCompany/**`, `neko-hoa/**`, `infra/modules/pr-environment/**`, `infra/environments/pr/**`, `.github/workflows/pr-env.yml` |
| Guard | `github.event.pull_request.head.repo.full_name == github.repository && github.event.pull_request.draft == false` |
| Concurrency | `group: pr-env-${{ github.event.number }}`, `cancel-in-progress: true` |
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

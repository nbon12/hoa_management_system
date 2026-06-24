# Implementation Plan: Ephemeral per-PR test environments

**Branch**: `013-ephemeral-pr-envs` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/013-ephemeral-pr-envs/spec.md`

## Summary

Every qualifying pull request gets its own disposable, production-like environment built from
the **real** stack — an isolated Neon database branch, an isolated Cloudflare R2 bucket, a
per-PR Cloud Run service, and a per-PR Cloudflare Pages preview — so PR checks exercise the
PR's actual code against real infrastructure before merge (shift-left). The approach
**parameterizes the existing IaC and CI** (the reusable `infra/modules/environment` pattern
and the `deploy-dev` smoke gate) rather than introducing a new stack: a new lightweight
`pr-environment` OpenTofu module creates only the cheap per-PR resources and references the
shared GCP project / Neon project / WIF / service accounts; three new GitHub Actions workflows
provision (on PR open/update), tear down (on close/merge), and sweep (daily) the environments.
Fork PRs never provision (GitHub-native external-contributor approval + a `pull_request`
head-repo guard). External integrations run in real test/sandbox mode with a live per-PR
Stripe webhook. Cost is capped by a checked-in `google_billing_budget` ($25/mo, alert at 80%).

## Technical Context

**Language/Version**: HCL for OpenTofu ≥ 1.8 (provisioning); GitHub Actions YAML + Bash (CI);
C# / .NET 9.0 backend and TypeScript / Angular 17.3 frontend reused **unchanged** except a
build-time API-URL injection on the frontend.
**Primary Dependencies**: OpenTofu providers `hashicorp/google ~5.0`, `hashicorp/google-beta ~5.0`,
`cloudflare/cloudflare ~4.0`, `kislerdm/neon =0.6.3`; `google-github-actions/auth@v2` (WIF);
`wrangler` (Pages); Stripe REST API (test mode); existing app (FastEndpoints, EF Core 9, Serilog, Sentry).
**Storage**: Per-PR Neon Postgres **branch** (copy-on-write from a pre-seeded `pr-base`); per-PR
Cloudflare **R2** bucket. No new application schema; existing migrations apply idempotently at startup.
**Testing**: `tofu fmt/validate/plan` + Trivy IaC scan for infra; the existing health gate +
Cypress (`e2e:dev`) + Playwright (`e2e:playwright-dev`) smoke suites run against the per-PR URLs;
backend xUnit/Testcontainers and frontend Karma unchanged.
**Target Platform**: Google Cloud Run (scale-to-zero), Cloudflare Pages + R2, Neon, all via GitHub Actions.
**Project Type**: Infrastructure + CI/CD feature on an existing web application (.NET API + Angular SPA).
**Performance Goals**: PR env ready ≤ 10 min p95 (SC-003); full teardown ≤ 30 min (SC-004);
orphans reclaimed within one daily sweep (SC-005).
**Constraints**: $25/mo PR-env budget, alert at 80% (SC-008); no production data in any env
(SC-009); fork/non-collaborator PRs never provision (FR-015); scale-to-zero everywhere.
**Scale/Scope**: Modest concurrency (solo/small-team); resource names namespaced by PR number;
per-PR GCS state prefix `state/pr/<n>`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ Reuses Angular on Cloudflare Pages, .NET FastEndpoints API on Cloud Run,
  Neon Postgres, Cloudflare R2, Docker Hub images, GitHub Actions, and the established OpenTofu
  IaC. No new platform technologies. Swashbuckle stays dev-only (PR envs run as `Dev`).
- **HOA tenancy**: ✅ N/A for new data — no schema change; per-PR DB carries the existing
  HOA-scoped schema and deterministic seed.
- **API contracts**: ✅ No new endpoints. Per-PR API behaves exactly as the app does; the
  existing dev-only e2e cleanup endpoint is reused. No contract changes, so no pagination/error
  shape impact.
- **Security and operations**: ✅ Secrets externalized in Secret Manager (per-PR `db-connection`,
  `stripe-webhook`; shared operator secrets reused); WIF (no long-lived keys); per-PR secrets never
  logged and deleted on teardown (FR-010); Auth0/Serilog/Sentry behavior inherited from the app
  unchanged. Production data isolation enforced (FR-011/SC-009).
- **File storage**: ✅ Hosted PR environments use **R2** (per the constitution's "hosted
  environments MUST use R2"). MinIO remains for local Docker Compose and local/CI unit tests. PR
  validation deliberately uses real R2 — this is the gap the feature closes — and is consistent
  with the hosted-environment rule (a PR env is a hosted environment, not a local/CI substitute).
- **Caching/edge**: ✅ Per-PR API is reached directly via its Cloud Run URL for smoke tests
  (no per-PR Cloudflare proxy needed for ephemeral test envs); no authenticated responses are
  edge-cached. Acceptable and justified for disposable test environments.
- **Testing discipline**: ✅ Persistence still runs on real Postgres (Neon branch); frontend uses
  Cypress/Playwright per the constitution. IaC is verified by `tofu validate`/`plan` + Trivy, and
  each acceptance scenario maps to a runnable workflow check (see research D12 / quickstart).
- **CI/CD and documentation**: ✅ Sonar + Codecov gates in `test.yml` are unaffected; Trivy scans
  the new HCL; the Repowise gate runs; deployment env isolation is the feature itself. Repowise
  outputs refreshed before PR.
- **Executable & living specs**: ✅ Acceptance scenarios are backed by the provisioning/teardown/sweep
  workflows and the smoke suites; `spec.md` was updated with the five clarifications; `spec.md` +
  `tasks.md` will be brought current before the PR. No contradiction with prior specs (the
  out-of-scope items — rate-limiter partitioning, smoke-scope curation, env-name gating — are
  explicitly deferred to a separate spec).

**Result**: PASS. No violations requiring Complexity Tracking. One coverage nuance noted below.

> **Coverage nuance (not a violation)**: the constitution's ≥95% changed-file coverage gate targets
> application code. This feature is predominantly HCL/YAML/Bash, which is verified by
> `tofu validate`/`plan`, Trivy, and end-to-end workflow execution rather than line coverage. The
> only app-code change (frontend API-URL injection) carries its existing unit/boot-guard coverage.

## Project Structure

### Documentation (this feature)

```text
specs/013-ephemeral-pr-envs/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions D1–D12
├── data-model.md        # Phase 1 — environment/lifecycle entities (no DB schema change)
├── quickstart.md        # Phase 1 — operator + contributor runbook
├── contracts/           # Phase 1 — module I/O, workflow, and Stripe-webhook contracts
│   ├── pr-environment-module.md
│   ├── workflows.md
│   └── stripe-webhook.md
└── tasks.md             # Phase 2 — created by /speckit.tasks (NOT here)
```

### Source Code (repository root)

```text
infra/
├── bootstrap/state-bucket/        # CHANGED: enable billingbudgets.googleapis.com API
├── modules/
│   ├── environment/               # UNCHANGED (shared Dev/Staging module, referenced)
│   └── pr-environment/            # NEW: lightweight per-PR module (D1)
│       ├── variables.tf  outputs.tf  versions.tf
│       ├── neon.tf                # branch (fork of pr-base) + endpoint + role + db
│       ├── cloud_run.tf           # nekohoa-api-pr-<n> service (labels, scale-to-zero)
│       ├── r2.tf                  # nekohoa-pr-<n>-documents bucket
│       └── secrets.tf             # pr-<n>-db-connection, pr-<n>-stripe-webhook
└── environments/
    ├── dev/                       # CHANGED: + google_billing_budget (pr-env label, $25 tfvar)
    └── pr/                        # NEW: root instantiating modules/pr-environment (pr_number var)
        ├── main.tf  variables.tf  backend.tf   # backend prefix via -backend-config at init

.github/workflows/
├── pr-env.yml                     # NEW: provision + smoke gate on PR open/update (D6)
├── pr-env-teardown.yml            # NEW: destroy on PR close/merge (D7)
└── pr-env-sweep.yml               # NEW: daily reclaim of orphans + 7-day-idle envs (D8)

scripts/
├── stripe-webhook-register.sh     # NEW: create test webhook, write signing secret (D9)
└── stripe-webhook-deregister.sh   # NEW: delete test webhook (D9)

neko-hoa/                          # CHANGED: build-time injection of per-PR API base URL (D5)
```

**Structure Decision**: This is an infrastructure + CI feature on the existing web app. It adds a
new OpenTofu module (`infra/modules/pr-environment`) and root (`infra/environments/pr`), three new
GitHub Actions workflows, and two small Stripe helper scripts; it changes the Dev infra root (budget),
the bootstrap (API enablement), and the frontend build (API-URL injection). Application backend code
is unchanged. Names and state are namespaced by `pr_number`.

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `infra/modules/pr-environment/*.tf` | `domain=pr-environment` | Per-PR resource set + isolation guarantees |
| `.github/workflows/pr-env*.yml` | `section=pr-env-ci` | Provision/teardown/sweep lifecycle |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> No constitution violations require justification. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 2 outlook (handed to /speckit.tasks)

Task generation should produce, in dependency order: (1) bootstrap + budget (shared, one-time);
(2) `modules/pr-environment` + `environments/pr` with `tofu validate`/`plan` green; (3) Stripe
webhook scripts; (4) `pr-env.yml` provisioning gate; (5) frontend API-URL injection; (6)
`pr-env-teardown.yml`; (7) `pr-env-sweep.yml`; (8) repo-settings runbook + Repowise/marker
updates; (9) spec/tasks freshness. P1 (DB+storage isolation) is independently shippable before
P2 (per-PR app instance) and P3 (teardown/cost), matching the spec's priority slices.

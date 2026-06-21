# Phase 0 Research: Ephemeral per-PR test environments

**Feature**: 013-ephemeral-pr-envs · **Date**: 2026-06-21

This document resolves the design decisions the spec leaves to planning. The spec's
five clarifications (provisioning trigger, 7-day inactivity lifetime, fork gate via
GitHub native trust, real-test-mode integrations with live webhooks, $25 budget-as-code)
are treated as settled inputs. Everything below is grounded in the **existing** infra
(`infra/modules/environment`), CI (`.github/workflows/*`), and app startup behavior.

## Existing-system facts this builds on

- **`infra/modules/environment/`** is a fully reusable, 100%-parameterized module
  (`env_name`, `secret_prefix`, `aspnet_environment`, …). Dev and Staging are thin
  instantiations. It provisions, per environment: a Neon **project**+branch+endpoint+role+db,
  a Cloud Run v2 service, a Cloudflare Pages project + R2 bucket + DNS, 9 Secret Manager
  secrets, 2 service accounts, and a WIF pool+provider.
- **WIF is repo-scoped, not ref-scoped** (`assertion.repository == "nbon12/hoa_management_system"`),
  so PR-triggered jobs already authenticate as the deployer SA. No new IAM needed for PRs.
- **Remote state**: single GCS bucket `nekohoa-dev-tfstate`, per-env prefix (`state/dev`,
  `state/staging`). Backend prefix is set in `backend.tf`.
- **App startup** (`Program.cs` → `StartupTasks`/`DatabaseSeeder`): applies EF migrations
  idempotently, then seeds **idempotently** (skips if `resident@nekohoa.dev` exists).
  `appsettings.Dev.json` enables `ApplyMigrations`, `SeedData`, `EnableSwagger`.
- **CORS already allows `*.pages.dev` preview origins in Dev** (PR #66). The deploy-dev job
  already deploys a Pages preview branch and runs Playwright/Cypress against
  `PLAYWRIGHT_BASE_URL` / `PLAYWRIGHT_API_URL`.
- **Config validation** validates `ASPNETCORE_ENVIRONMENT` against the known set
  `{Development, Dev, Test, Staging, Production}` and fails fast otherwise.

---

## D1 — Provisioning granularity: a new lightweight `pr-environment` module

**Decision**: Add `infra/modules/pr-environment/` that creates **only the cheap, per-PR
resources** and **references the shared primitives** the Dev environment already owns.

Per-PR (created/destroyed each PR):
- Neon **branch** (forked, copy-on-write) + pooled endpoint + role + database
- Cloud Run v2 **service** `nekohoa-api-pr-<n>` (scale-to-zero, public invoker)
- Cloudflare **R2 bucket** `nekohoa-pr-<n>-documents`
- Per-PR Secret Manager secrets: `pr-<n>-db-connection`, `pr-<n>-stripe-webhook`
- GCP resource **labels** `pr-env=true`, `pr-number=<n>` (drives budget filter + sweep)

Shared / referenced (never recreated per PR):
- The GCP project, the Neon **project** (`super-water-18090867`), the WIF pool/provider,
  the runtime + deployer **service accounts**, and the 8 shared operator secrets
  (jwt, sentry, **Stripe test** secret, storage access/secret keys, scheduler).

**Rationale**: Reusing the full `environment` module per PR would recreate a Neon *project*,
two SAs, a WIF pool, and 9 secrets on every PR — slow (blows the 10-min SC-003 budget),
quota-hungry, and pointless since those are stable shared primitives. Forking a Neon branch
is ~1–2s (copy-on-write); a Cloud Run service and R2 bucket are seconds. This keeps a PR
env to the few resources that genuinely must be isolated.

**Alternatives rejected**:
- *Instantiate the existing `environment` module with `env_name="pr-<n>"`* — recreates
  heavyweight shared infra per PR; ~minutes and quota pressure.
- *Single shared Cloud Run service with per-PR revision tags* — can't isolate env
  vars/secrets/DB per PR cleanly; teardown and budget attribution get muddy.

## D2 — State isolation: new `infra/environments/pr/` root, prefix `state/pr/<n>`

**Decision**: A new root `infra/environments/pr/` instantiates `modules/pr-environment`
with a `pr_number` variable. The GCS backend prefix is **`state/pr/<n>`**, supplied at
`tofu init` via `-backend-config="prefix=state/pr/${PR_NUMBER}"` (the rest of the backend
block — bucket — stays static in `backend.tf`). Teardown is `tofu destroy` against the
same prefix, then the state object is deleted.

**Rationale**: Per-PR prefixes give each environment its own isolated state lineage in the
existing bucket (FR-002), make teardown a deterministic `destroy` (FR-008), and let the
sweep enumerate live envs by listing `state/pr/*`. Reuses the existing state bucket and
the FR-020 "one bucket, many prefixes" pattern.

**Alternatives rejected**: Terraform *workspaces* (less explicit than prefixes here, and
the team already standardized on prefix-per-env); a separate bucket per PR (needless).

## D3 — Database: per-PR Neon branch forked from a pre-seeded base

**Decision**: Maintain one long-lived **`pr-base`** Neon branch (Dev-shaped schema, seeded
once with the deterministic seed). Each PR forks `pr-base` → `pr-<n>` (copy-on-write,
instant, already carrying seed data). On container startup the app **applies migrations
idempotently** (so the PR's own new migrations land) and the **seeder no-ops** (idempotency
check finds the seeded user). No production data ever touches a branch (FR-011, SC-009).

**Rationale**: Forking a pre-seeded base gives a deterministic, ready DB in ~1–2s
(SC-003) without a per-PR seed step, while still exercising the PR's new migrations on top.
Copy-on-write means storage cost is just the diff (pennies — matches the cost analysis).

**Alternatives rejected**: Fork from the live Dev `main` branch then seed at startup —
slower, and risks branching off mutated Dev data (non-deterministic). Empty branch +
full migrate+seed every PR — adds minutes.

**Open follow-up**: `pr-base` must be refreshed when the seed/schema changes materially;
the sweep workflow re-creates it from Dev if absent (self-healing).

## D4 — Application instance: one Cloud Run service per PR

**Decision**: `nekohoa-api-pr-<n>`, image `sakurapatch/nekohoa-api:pr-<n>-<sha>` (built by
the PR workflow), `ASPNETCORE_ENVIRONMENT=Dev`, scale-to-zero, **public invoker**
(`allUsers`, like Dev) so smoke tests reach it. Runs as the **shared runtime SA**. Wiring:
- `ConnectionStrings__DefaultConnection` ← `pr-<n>-db-connection` secret (Neon branch pooled)
- `Stripe__WebhookSecret` ← `pr-<n>-stripe-webhook` secret (see D9)
- `Storage__BucketName=nekohoa-pr-<n>-documents`, `Storage__ForcePathStyle=true`
- All other operator secrets + `Stripe:PublishableKey` ← **shared** Dev values (test mode)

**Rationale**: A service-per-PR is the clean isolation + teardown unit (delete the service,
it's gone). Running as `Dev` reuses every Dev behavior (migrations on, seed on, Swagger on,
CORS preview-origin allowance) and stays inside the config-validation known set — **no new
environment name**, so no new appsettings/validator. The shared runtime SA + per-PR
*secret values* gives isolation without per-PR IAM churn.

**Alternatives rejected**: A new `Pr` environment name (requires `appsettings.Pr.json` +
validator changes + CORS rule duplication for no benefit); per-PR runtime SA (unnecessary).

## D5 — Frontend: reuse the `nekohoa-dev` Pages project, per-PR branch deploy

**Decision**: Reuse the existing `nekohoa-dev` Cloudflare Pages project. Deploy each PR as a
**branch deploy** `--branch=pr-<n>`, yielding the stable alias `pr-<n>.nekohoa-dev.pages.dev`.
The PR's **API base URL** (the PR Cloud Run URL) is injected into the Angular build for that
deploy. CORS on the PR API already allows `*.pages.dev` (PR #66), satisfying FR-005.

**Rationale**: Pages branch deploys give a stable, unique per-PR URL with zero project
churn and no quota cost (previews are free). Matches the existing deploy-dev preview
mechanism, just keyed by PR number instead of SHA.

**Alternatives rejected**: A Pages project per PR (creation overhead, project-count limits).

## D6 — Provisioning workflow `pr-env.yml`

**Decision**: New workflow on `pull_request` (`types: [opened, synchronize, reopened, ready_for_review]`),
`paths: [HOAManagementCompany/**, neko-hoa/**, infra/modules/pr-environment/**, infra/environments/pr/**, .github/workflows/pr-env.yml]`.
Guard: `if: github.event.pull_request.head.repo.full_name == github.repository && github.event.pull_request.draft == false`.
Concurrency: `group: pr-env-${{ github.event.number }}`, `cancel-in-progress: true`.
Jobs (sequential gates, mirrors deploy-dev): build+push image (`pr-<n>-<sha>`) → `tofu apply`
the PR env (D1/D2) → register Stripe test webhook (D9) → build+deploy Pages branch (D5) →
poll `/health` → Cypress + Playwright against the PR URLs → post a sticky PR comment with
the env URLs and status.

**Rationale**: `pull_request` (not `pull_request_target`) means **secrets are withheld from
fork runs** automatically (FR-015); the head-repo + non-draft guard implements the Q1/Q3
clarifications exactly. Path filter skips doc/spec-only PRs (FR-012). Reuses the proven
deploy-dev gate ordering.

**Alternatives rejected**: `pull_request_target` (exposes secrets to fork code); a label
trigger (the clarification chose path-filtered auto, no label).

## D7 — Teardown workflow `pr-env-teardown.yml`

**Decision**: New workflow on `pull_request: [closed]` (covers both merge and close), same
head-repo guard. Steps (idempotent, best-effort each): `tofu destroy` prefix `state/pr/<n>`
→ delete the GCS state object → deregister the Stripe webhook (D9) → delete the Pages
`pr-<n>` branch deploy → delete the `pr-<n>-*` image tags. Targets a 30-min window (SC-004).

**Rationale**: `closed` fires on merge and close alike. `tofu destroy` from the per-PR state
is the authoritative teardown (FR-006). Each external cleanup is best-effort so one failure
doesn't strand the rest; the sweep (D8) backstops any leak.

## D8 — Reclaim + orphan sweep `pr-env-sweep.yml`

**Decision**: Scheduled workflow (cron `13 7 * * *`, off the :00 mark) plus `workflow_dispatch`.
Enumerate live PR envs by listing `state/pr/*` prefixes in the state bucket. For each PR number:
- **Orphan** (PR is closed/merged but env still present) → `tofu destroy` (FR-008, SC-005).
- **Inactive** (PR open but last commit > **7 days** ago, via GitHub API) → `tofu destroy` +
  post a comment that the env was reclaimed and will rebuild on the next push (FR-007).
- Ensure the `pr-base` Neon branch exists; recreate from Dev if missing (self-healing, D3).

**Rationale**: The state-prefix list is the source of truth for "what exists"; cross-referencing
open-PR + last-commit-date implements the 7-day-inactivity (no absolute cap) clarification and
the orphan guarantee. Re-up is automatic on the next qualifying push (pr-env.yml) or a manual
workflow re-run, per FR-007.

## D9 — Stripe per-PR webhook (real test-mode end-to-end, FR-013)

**Decision**: At provision, a script step calls the **Stripe API (test mode)** to create a
webhook endpoint pointing at the PR API's webhook path (`https://<pr-api-url>/api/v1/payments/webhook`),
capture its **signing secret**, and write it to the `pr-<n>-stripe-webhook` Secret Manager
secret the container consumes. At teardown (D7) and reclaim (D8), delete that endpoint.
SendGrid and Twilio run in **sandbox/test** mode **outbound-only** — no inbound endpoint is
needed for the in-scope flows; their shared sandbox credentials are reused.

**Rationale**: A per-PR live webhook endpoint is what makes payment flows genuinely
end-to-end (the Q4 clarification) without real charges. Stripe has no first-class OpenTofu
provider in this stack, so a thin API script (create on provision, delete on teardown) is the
pragmatic, reproducible mechanism; the signing secret lives only in Secret Manager (FR-010).

**Alternatives rejected**: Stripe CLI `listen` forwarding inside the test job (not a real
inbound endpoint; dies with the job); stubbing webhooks (the clarification rejected stubs).

## D10 — Cost guardrail: `google_billing_budget` as code (SC-008)

**Decision**: Add a **single** `google_billing_budget` (account-level, created once) in the
`infra/environments/dev` root — filtered to GCP costs
carrying the **`pr-env=true`** label, `amount` from tfvar `pr_env_monthly_budget` (default
**25**), threshold rules at **0.8** and **1.0**, notifying an email/Pub/Sub channel. Bootstrap
enables the `billingbudgets.googleapis.com` API; the billing account id is a new input.
Alert-only — **no** automatic billing hard-cap (out of scope per the clarification).

**Rationale**: Budgets are billing-account scoped, so one resource (not per-PR) filtered by
label is correct. Cloud Run is the only meaningful variable GCP cost (CI minutes are free on
the public repo's standard runners; R2/Neon are negligible and not in GCP billing), so a
label-filtered GCP budget captures essentially all of it. Defined in the existing OpenTofu and
validated by the existing `infra-plan`/Trivy pipeline (SC-008 "validated by existing IaC checks").

## D11 — Fork safety & secret scoping (FR-010, FR-015)

**Decision**: Defense in depth — (1) `on: pull_request` withholds secrets from fork runs;
(2) the `head.repo.full_name == github.repository` job guard skips forks entirely;
(3) the repo's **"Require approval for all external contributors"** Actions setting blocks
*all* workflow runs from non-collaborators until a maintainer approves; (4) infra secrets are
scoped to a **required-reviewer GitHub Environment**; (5) `GITHUB_TOKEN` default is already
read-only. Per-PR secrets live only in Secret Manager, are never echoed to logs, and are
deleted on teardown/reclaim.

**Rationale**: Directly encodes the Q3 clarification using GitHub-native trust (collaborator
status), no hand-maintained allowlist. Items (1)–(2) are workflow code in this feature; (3)–(5)
are one-time repo/ops settings captured in `quickstart.md`.

## D12 — Environment name & verification mapping

**Decision**: PR envs use `ASPNETCORE_ENVIRONMENT=Dev`. Each mandatory acceptance scenario maps
to an automated check already in the stack: provisioning/health (workflow `/health` gate),
storage-incompat detection (Playwright payment/document flows against real R2), isolation
(distinct Neon branch + R2 bucket per PR), teardown/orphan (teardown + sweep workflows, asserted
by a destroy that leaves zero labeled resources). No new app endpoints; the existing e2e cleanup
endpoint (gated by `DevTools.E2ECleanupEnabled`) is reused.

---

## Summary of new vs. changed artifacts

| Area | New | Changed |
|------|-----|---------|
| OpenTofu | `infra/modules/pr-environment/**`, `infra/environments/pr/**` | `infra/environments/dev` (+ `google_billing_budget`), `infra/bootstrap` (+billingbudgets API) |
| Workflows | `pr-env.yml`, `pr-env-teardown.yml`, `pr-env-sweep.yml` | — |
| Frontend | — | Angular build: inject per-PR API base URL |
| Backend | — | None expected (CORS preview-origin + e2e cleanup already exist) |
| Scripts | Stripe webhook register/deregister; (optional) URL-comment helper | — |
| Repo settings (ops, not code) | — | "Require approval for all external contributors"; required-reviewer Environment for infra secrets |

All NEEDS CLARIFICATION resolved. No open unknowns block Phase 1.

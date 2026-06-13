# Phase 0 Research: Dev Environment Auto-Deploy

**Feature**: 009-dev-auto-deploy | **Date**: 2026-06-13

This document records the decisions that resolve the open technical questions for deploying an
isolated Dev environment automatically on merge to `main`. All `/speckit.clarify` questions were
already answered (see spec Clarifications); the items below are platform/implementation decisions.

---

## D1 — Where the deploy stage lives in CI

**Decision**: Append a new `deploy-dev` job to the existing `.github/workflows/test.yml`, with
`needs: [docker-push]` and `if: github.ref == 'refs/heads/main' && github.event_name == 'push'`.

**Rationale**: `test.yml` already runs `test → integration-sandbox → docker-push` on push to
`main` and publishes `sakurapatch/nekohoa-api:${{ github.sha }}` to Docker Hub. A job in the **same** workflow
can `needs` those jobs directly; a separate workflow cannot express that dependency without fragile
`workflow_run` chaining. Reusing the published `:${sha}` image means the deployed artifact is
exactly what passed CI (FR-002, FR-018).

**Alternatives considered**:
- *Separate `deploy-dev.yml` triggered by `workflow_run`* — rejected: indirection, harder failure
  attribution, and re-resolves the image tag.
- *Build the image inside the deploy job* — rejected: duplicates `docker-push`, risks a different
  artifact than the one CI validated.

---

## D2 — "Failed deploy never takes down Dev" mechanism (backend)

**Decision**: Deploy the new backend revision to Cloud Run with `--no-traffic --tag=candidate`,
verify it (health + E2E) via its **tagged revision URL**, then promote with
`gcloud run services update-traffic nekohoa-api-dev --to-tags candidate=100`. On any failure, skip
the promote step — 100% of traffic stays on the previous healthy revision.

**Rationale**: Cloud Run revision tagging gives a verifiable, zero-traffic candidate URL. This
satisfies FR-006/FR-007/SC-003 (no downtime on failure) **without** an explicit rollback: a failed
candidate simply never receives traffic and is later garbage-collected.

**Known constraint (documented)**: EF Core migrations run at **candidate startup**, before
promotion, against the shared Dev database. A migration therefore lands as soon as the candidate
boots, even if E2E later fails. This is acceptable because migrations are additive/backward-
compatible by policy; **destructive migrations require a documented rollback/mitigation plan**
(FR-017, Constitution §3). Captured as an edge case in the spec.

**Alternatives considered**:
- *Deploy with immediate 100% traffic, roll back on failure* — rejected: causes a downtime/breakage
  window and a race on concurrent merges.
- *Run migrations as a separate pre-deploy job* — rejected: diverges from the constitution's
  "Cloud Run startup applies migrations idempotently" and the existing startup seeding path.

---

## D3 — "Failed deploy never takes down Dev" mechanism (frontend)

**Decision**: Build the Angular app with a new `dev` configuration and deploy to Cloudflare Pages
as a **preview/branch deployment**; run E2E against the preview URL; promote the preview to the
Dev production alias only on pass (via `wrangler pages deploy` to the project's production branch,
or `cloudflare/pages-action` with the branch set to the Pages production branch).

**Rationale**: Pages preview deployments are immutable and addressable, mirroring the Cloud Run
candidate pattern so the frontend is verified before it becomes the live Dev site (FR-005, FR-007).

**Alternatives considered**:
- *Deploy straight to production alias* — rejected: a broken build would replace the working Dev
  site before E2E runs.

---

## D4 — Backend environment name + config-driven startup

**Decision**: Run the Dev service under a dedicated `ASPNETCORE_ENVIRONMENT=Dev`. Refactor
`Program.cs` so the following are **configuration-driven** instead of gated on `IsDevelopment()`:
- `Startup:ApplyMigrations` (bool) — run `MigrateAsync` at startup. Default **true** for
  `Development` and `Dev`.
- `Startup:SeedData` (bool) — run `DatabaseSeeder.SeedAsync` (migrate + idempotent seed) at
  startup. Default **true** for `Development` and `Dev`.
- `Startup:EnableSwagger` (bool) — expose `/swagger`. Default **true** for `Development`/`Dev`,
  **false** in `Production` (Constitution §4).
- `Cors:AllowedOrigins` (string[]) — replace **only** the hardcoded `localhost:4200` origins in the
  CORS policy; Dev sets the Cloudflare Pages Dev origin, local `Development` keeps localhost
  defaults. **Preserve the explicit `.WithHeaders(...)`/`.WithMethods(...)` lists** introduced by
  PR #28 (`fix(security): replace AllowAnyHeader/AllowAnyMethod`, SonarCloud S5122) — do **not**
  reintroduce `AllowAnyHeader()`/`AllowAnyMethod()`, or the now-**enforced** Sonar quality gate
  will block the PR.

Relax the `--seed` CLI guard and the startup seed block to allow `Dev` as well as `Development`.

**Rationale**: A deployed service must **not** run as `Development` — that enables developer
exception pages (leaks internals) and the hardcoded localhost-only CORS, which would block the
deployed Dev frontend. Making these toggles explicit keeps current local behavior unchanged
(defaults preserve it) while letting Dev be a first-class deployed environment with production-safe
errors but dev conveniences (Swagger, auto-migrate, seed). Aligns with Constitution §3/§4/§8.

**Alternatives considered**:
- *Reuse `ASPNETCORE_ENVIRONMENT=Development` on Cloud Run* — rejected: leaks dev exception pages,
  breaks CORS, and conflates local vs deployed semantics.
- *Hardcode a second `IsEnvironment("Dev")` branch* — rejected: duplicates logic and doesn't
  generalize to Staging/Prod migration-at-startup later.

---

## D5 — Database isolation (Neon Dev)

**Decision**: Use a dedicated Neon **Dev database** (separate project/branch from Staging/Prod),
injected as `ConnectionStrings__DefaultConnection` from a managed secret. Keep Npgsql pooling with
a **low max pool size** and the existing `EnableRetryOnFailure(3)`; the data source is owned and
disposed by DI (already the case). Neon scale-to-zero is enabled for Dev.

**Rationale**: Constitution §10 requires separate databases per environment; §8 requires low max
connections, pooling, short-lived `DbContext`. The app already registers a single traced
`NpgsqlDataSource` and scoped `DbContext` — only the connection string and a `Maximum Pool Size`
parameter change for Dev.

**Alternatives considered**:
- *Share a database across environments with schema prefixes* — rejected: violates §10 isolation
  and risks cross-environment data contact (SC-005).

---

## D6 — Frontend hosting + Dev API URL

**Decision**: New Cloudflare Pages project for Dev serving `dist/neko-hoa/browser`. Add
`environment.dev.ts` with `apiBaseUrl`/`telemetryUrl` pointing at the Dev API custom domain
(`https://api-dev.nekohoa.com/api/v1`) fronted by Cloudflare → Cloud Run, and the **Stripe test**
publishable key. Add a `dev` build configuration in `angular.json` that file-replaces
`environment.ts` with `environment.dev.ts` (mirrors the existing `production`/`development`/`docker`
configs) and keeps `outputHashing: all`.

**Rationale**: The app already uses build-time `fileReplacements` for environments; Dev is one more
configuration. A stable `api-dev` custom domain (rather than the raw Cloud Run URL) keeps the edge
in front and the frontend config stable across revisions (FR-005, FR-015).

**Alternatives considered**:
- *Point the frontend at the raw Cloud Run URL* — rejected: bypasses the Cloudflare edge (FR-015)
  and the URL is less stable.
- *Runtime-injected config* — rejected: Angular build-time replacement is the established pattern
  here; no need to introduce runtime config loading.

---

## D7 — E2E gate against deployed Dev

**Decision**: After both backend (candidate) and frontend (preview) are up, run the E2E suite
against the Dev URLs as the promotion gate. Cypress is the primary suite (`cypress run` with
`--config baseUrl=<dev-frontend-url>` and the API base via env); Playwright `e2e` may run as a
second lens. Authentication uses the **seeded** login user (`DatabaseSeeder` already creates a
login-able user); Stripe stays in **test mode** (Dev backend holds Stripe test keys, and the
existing `window.Stripe` seam keeps card entry deterministic).

**Rationale**: Clarification Q3 chose the full E2E suite as the gate. Reusing the seeded user and
the test-mode Stripe seam keeps the run deterministic and avoids real charges. A failing suite
blocks promotion of both candidate revision and preview (FR-006, SC-010).

**Alternatives considered**:
- *Readiness probe only* — rejected by clarification.
- *Spin up ephemeral data per run* — unnecessary: idempotent seed already provides a known dataset
  (FR-004a).

---

## D8 — Concurrency / latest-commit-wins

**Decision**: Add `concurrency: { group: deploy-dev, cancel-in-progress: true }` to the
`deploy-dev` job. The Cloud Run/Pages promote step always targets the revision/preview built from
the **current** run's `${{ github.sha }}`.

**Rationale**: With `cancel-in-progress: true`, a newer merge cancels an in-flight older deploy, so
the latest commit ends up live (FR-009, SC-007). Promotion is the last step, so a cancelled older
run never promotes a stale revision.

**Alternatives considered**:
- *Queue without cancel* — rejected: an older commit could promote after a newer one, leaving Dev
  stale.

---

## D9 — CI authentication to GCP and Cloudflare

**Decision**: Authenticate to GCP from GitHub Actions via **Workload Identity Federation**
(`google-github-actions/auth` with a WIF provider) — no long-lived JSON key. Authenticate to
Cloudflare with a scoped **API token** (`CLOUDFLARE_API_TOKEN`) + `CLOUDFLARE_ACCOUNT_ID`. Docker
Hub auth already exists (`DOCKER_HUB_USERNAME`/`DOCKER_HUB_TOKEN`).

**Rationale**: WIF avoids storing exportable service-account keys (SC-006, Constitution §8). Scoped
tokens limit blast radius.

**Alternatives considered**:
- *`GCP_SA_KEY` JSON secret* — acceptable fallback if WIF setup is blocked, but rejected as the
  default because it's a long-lived exportable credential.

---

## D10 — Failure notification channel

**Decision**: On any `deploy-dev` failure, post to a team chat channel via an incoming
**webhook secret** (`DEPLOY_ALERT_WEBHOOK_URL`) in an `if: failure()` step. Success relies on the
GitHub Actions run + deployment status (no chat spam). Channel is Slack-style by default; the
webhook URL is provider-agnostic.

**Rationale**: Clarification Q4 chose "GitHub status always + chat on failure" (FR-008, SC-008).

**Alternatives considered**:
- *Email* — rejected: noisier, slower to act on than a chat ping.
- *Chat on every deploy* — rejected by clarification (noise).

---

## Runtime secret inventory (Dev) — resolved at deploy/run time, never committed

| Secret (env var) | Source | Purpose |
|------------------|--------|---------|
| `ConnectionStrings__DefaultConnection` | Secret Manager | Neon Dev database |
| `Jwt__Secret` | Secret Manager | App JWT signing key (Dev-only value) |
| `Sentry__Dsn` | Secret Manager | Dev Sentry project |
| `Stripe__SecretKey` / `Stripe__WebhookSigningSecret` | Secret Manager | Stripe **test** mode |
| `Storage__*` (R2 endpoint/access/secret/bucket) | Secret Manager | Cloudflare R2 Dev bucket |
| `Jobs__SchedulerSharedSecret` | Secret Manager | Scheduler auth |
| `Twilio__*`, `SendGrid__*` | Secret Manager (optional) | Alerts (test mode / may be unset) |
| `OTEL_EXPORTER_OTLP_*` | Cloud Run env / secret | Telemetry target + headers |

GitHub Actions deploy-time secrets: WIF provider + GCP service account, `CLOUDFLARE_API_TOKEN`,
`CLOUDFLARE_ACCOUNT_ID`, `DEPLOY_ALERT_WEBHOOK_URL`, plus existing Docker Hub secrets.

---

## D11 — Impact of merged PR #28 (SonarCloud hardening) — added 2026-06-13

PR #28 (`fix(sonar): run scan after tests; add sonar-project.properties…`) merged to `main` while
this feature was being planned. It changes files this feature edits. Decisions:

- **Sonar quality gate is now blocking.** `test.yml` dropped `continue-on-error: true`, runs the
  scan **after** tests, and adds a `sonarqube-quality-gate-action` step (both gated on
  `env.SONAR_HOST_URL != ''`). Because `deploy-dev` chains `needs: docker-push` →
  `needs: [test, integration-sandbox]`, a failed Sonar gate now blocks the deploy. This *is* the
  intended behavior (Constitution §9 — required checks pass before deploy); no change needed beyond
  recording it.
- **CORS is explicit-list now (S5122).** `Program.cs` no longer uses
  `AllowAnyHeader()`/`AllowAnyMethod()`. The T009 refactor makes **only origins** config-driven and
  keeps the explicit header/method lists (see D4).
- **Pin new third-party actions to commit SHAs (S6719).** PR #28 pinned the Sonar actions to SHAs.
  The new `deploy-dev` actions (`google-github-actions/auth`, `setup-gcloud`, the Cloudflare
  Pages/wrangler action) MUST likewise be pinned to a commit SHA with the version as a trailing
  comment, for consistency and to avoid a supply-chain hotspot.
- **No hardcoded credentials (S2068).** `appsettings.Dev.json` (T011) MUST contain **no secret
  values** — only non-secret `Startup` flags and `Cors:AllowedOrigins`. All Dev secrets come from
  Secret Manager at deploy/run time (already the design, SC-006). `sonar-project.properties`
  excludes `appsettings.Development.json`/`appsettings.Test.json` but **not** `appsettings.Dev.json`
  — fine, because it holds nothing secret to flag.
- **Coverage gate on new code.** `sonar.coverage.exclusions` covers `**/Program.cs` (so the
  `Program.cs` edits are exempt) but **not** the new `StartupOptions.cs` — the T012 xUnit tests must
  cover it to satisfy the 90% diff-coverage gate.

**All NEEDS CLARIFICATION resolved.** Ready for Phase 1.

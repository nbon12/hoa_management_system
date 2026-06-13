# Implementation Plan: Dev Environment Auto-Deploy on Merge to Main

**Branch**: `009-dev-auto-deploy` | **Date**: 2026-06-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/009-dev-auto-deploy/spec.md`

## Summary

Stand up an isolated **Dev** environment that is automatically deployed on every merge to
`main`, with **zero manual steps**. The existing CI already builds and pushes
`sakurapatch/nekohoa-api:{latest,sha}` to Docker Hub on merge to `main` — this feature adds the missing
**deploy** stage: release the backend image to an isolated Dev **Cloud Run** service (which
applies EF Core migrations and seeds reference/synthetic data at startup against an isolated
**Neon** Dev database), deploy the Angular frontend to a Dev **Cloudflare Pages** project pointed
at the Dev API, gate promotion on a **health check + full E2E suite run against Dev**, keep the
previous healthy release serving on any failure, and notify the team chat on failure.

The technical approach uses **Cloud Run revision tagging** (`--no-traffic --tag=candidate`) and
**Cloudflare Pages preview deployments** so a new release is verified by the full E2E suite
*before* it receives Dev traffic — satisfying "failed deploys never take down Dev" (FR-006/FR-007)
without an explicit rollback step. A small backend refactor makes migrations/seeding/Swagger/CORS
**configuration-driven** (today they are hardcoded to `IsDevelopment()`), so the deployed Dev
service behaves correctly under a dedicated `Dev` environment name without leaking
`Development`-only behavior (e.g., localhost-only CORS, developer exception pages).

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend); GitHub
Actions YAML + Bash (pipeline)
**Primary Dependencies**: FastEndpoints, EF Core 9 (Npgsql), Serilog, Sentry; Angular CLI;
`gcloud` CLI (Cloud Run), `wrangler`/`cloudflare/pages-action` (Cloudflare Pages),
`docker/build-push-action` (already in use)
**Storage**: PostgreSQL — isolated **Neon Dev** database (separate from Staging/Prod); Cloudflare
**R2** Dev bucket for documents (MinIO remains local/test only). No new tables or schema changes.
**Testing**: xUnit + Testcontainers.PostgreSQL (backend, existing); Cypress + Playwright E2E run
against the deployed Dev URL as the promotion gate
**Target Platform**: Linux containers on Google **Cloud Run** (scale-to-zero) behind **Cloudflare**
edge; static frontend on **Cloudflare Pages**
**Project Type**: Web application (backend `HOAManagementCompany/` + frontend `neko-hoa/`) +
CI/CD infrastructure
**Performance Goals**: Merged change live in Dev within **30 minutes** inclusive of the full E2E
gate (SC-002); Dev scales to zero when idle (cold start acceptable)
**Constraints**: Zero downtime of the prior healthy Dev release during a failed deploy (SC-003);
no secrets in repo or image layers (SC-006); isolated Dev DB/secrets (SC-005); latest merged
commit always wins on concurrent merges (SC-007/FR-009)
**Scale/Scope**: Single Dev environment; ~1 backend service + 1 frontend project; pipeline
changes plus a focused backend startup-config refactor and one new Angular build configuration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ GitHub Actions CI/CD, Docker → Docker Hub, Cloud Run, Neon, Cloudflare
  (edge + Pages + R2), Sentry are all used as the constitution mandates. **Swashbuckle/Swagger**
  is gated OFF in production and MAY be ON in Dev (made config-driven here). **Auth0 note**: the
  codebase currently authenticates with an app-issued **JWT bearer** scheme (symmetric key), not
  Auth0 — a *pre-existing* divergence from the constitution that this feature does not change.
  "Dev is Auth0-gated" (clarification) is implemented as **the same identity model the app uses in
  every other environment** (app-issued JWT), reachable behind the Cloudflare edge. Flagged, not
  introduced, by this feature; no new auth work.
- **HOA tenancy**: N/A — no new data entities or queries; existing tenant boundaries are
  unaffected.
- **API contracts**: N/A — no new endpoints. The existing `/health` endpoint is reused as the
  readiness gate.
- **Security and operations**: ✅ Secrets externalized to GitHub Actions secrets (deploy-time) and
  Cloud Run secret refs / Google Secret Manager (run-time); never committed or baked into images.
  Serilog structured logs and Sentry tracing already wired; this feature sets the Dev
  **environment** and **release** tags. Production-safe errors preserved (the `Dev` env name does
  **not** enable developer exception pages).
- **File storage**: ✅ Dev documents go to a Cloudflare **R2** Dev bucket via the existing S3
  client; PostgreSQL stores metadata only; MinIO stays local/test.
- **Caching/edge**: ✅ Cloudflare fronts the Dev API; authenticated responses are not edge-cached;
  frontend assets use Angular `outputHashing: all` (hashed filenames).
- **Testing discipline**: ✅ The full E2E suite gates promotion. The backend startup-config
  refactor is covered by xUnit tests (migrations/seed/Swagger gated by flags; CORS origins from
  config). Existing persistence tests keep Testcontainers + transaction isolation.
- **CI/CD and documentation**: ✅ Deployment runs automatically on merge to `main` with
  environment isolation (separate Dev service + DB + secrets). Repowise markers in `Program.cs`
  are refreshed if touched; the `repowise-gate` job continues to run on PRs.
  **Updated for merged PR #28 (SonarCloud hardening):** the SonarQube **quality gate is now
  blocking** (`continue-on-error` removed; gated on `SONAR_HOST_URL`), and because `deploy-dev`
  chains `needs: docker-push` ← `[test]`, the Sonar gate now also gates deploys (intended). New
  deploy actions MUST be **pinned to commit SHAs** (S6719); CORS stays an **explicit** header/method
  allow-list (S5122); `appsettings.Dev.json` MUST hold **no secrets** (S2068). The new
  `StartupOptions.cs` is not coverage-excluded (unlike `Program.cs`), so its tests must satisfy the
  90% diff-coverage gate.

**Result**: PASS — no violations requiring justification. (See Complexity Tracking: empty.)

## Project Structure

### Documentation (this feature)

```text
specs/009-dev-auto-deploy/
├── plan.md              # This file (/speckit.plan output)
├── research.md          # Phase 0 output — deployment & refactor decisions
├── data-model.md        # Phase 1 output — config/secrets/pipeline "entities" (no DB schema)
├── quickstart.md        # Phase 1 output — provision + verify the Dev environment
├── contracts/
│   ├── pipeline-contract.md      # GitHub Actions deploy-dev job: triggers, gates, promotion
│   └── environment-matrix.md     # Dev vs Staging/Prod config + secret inventory
└── checklists/
    └── requirements.md  # Spec quality checklist (already complete)
```

### Source Code (repository root)

```text
.github/workflows/
└── test.yml                      # MODIFIED — add `deploy-dev` job (needs docker-push),
                                   #   concurrency group (latest-wins), failure notification

HOAManagementCompany/             # Backend (.NET 9)
├── Program.cs                    # MODIFIED — config-driven startup: migrations, seed, Swagger,
│                                 #   and CORS allowed-origins (replace hardcoded IsDevelopment())
├── Infrastructure/Configuration/
│   └── StartupOptions.cs         # NEW — typed options (ApplyMigrations, SeedData, EnableSwagger)
├── appsettings.json              # MODIFIED — default Startup/Cors keys
└── appsettings.Dev.json          # NEW — Dev env-name defaults (Swagger on, migrate+seed on)

HOAManagementCompany.Tests/
└── Startup/StartupConfigTests.cs # NEW — assert flags gate behavior across env names

neko-hoa/                         # Frontend (Angular 17.3)
├── angular.json                  # MODIFIED — add `dev` build configuration
└── src/environments/
    └── environment.dev.ts        # NEW — Dev apiBaseUrl / telemetry / Stripe test pk
```

**Structure Decision**: Existing monorepo with a .NET backend (`HOAManagementCompany/`) and an
Angular frontend (`neko-hoa/`). This feature is overwhelmingly **CI/CD + configuration**: one new
`deploy-dev` job appended to the existing `test.yml` pipeline (so it can `needs: [docker-push]`),
a focused backend startup-config refactor to make Dev a first-class deployed environment, and one
new Angular build configuration. No new source modules, services, or database schema are
introduced.

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Program.cs` | `domain=bootstrap` | Update the existing bootstrap region note to describe config-driven migrations/seed/Swagger/CORS gating. |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

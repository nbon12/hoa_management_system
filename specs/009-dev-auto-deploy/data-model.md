# Phase 1 Data Model: Dev Environment Auto-Deploy

**Feature**: 009-dev-auto-deploy | **Date**: 2026-06-13

> **No database schema changes.** This is an infrastructure/CI-CD feature — it introduces **no new
> tables, columns, or EF Core migrations**. The "entities" below are configuration and pipeline
> constructs. They are modeled here so tasks, contracts, and validation have a shared vocabulary.

---

## Configuration entities

### StartupOptions (`HOAManagementCompany/Infrastructure/Configuration/StartupOptions.cs`)

Typed options bound from the `Startup` configuration section. Controls deployed-environment
startup behavior that is currently hardcoded to `IsDevelopment()`.

| Field | Type | Default (Development/Dev) | Default (Production) | Validation |
|-------|------|---------------------------|----------------------|------------|
| `ApplyMigrations` | bool | `true` | `false` | — |
| `SeedData` | bool | `true` | `false` | If `true`, `ApplyMigrations` is implied (seed migrates first) |
| `EnableSwagger` | bool | `true` | `false` | MUST be `false` in Production (Constitution §4) |

**State/lifecycle**: read once at startup. When `SeedData` (or `ApplyMigrations`) is true, the
service applies migrations idempotently before serving traffic; seeding is idempotent (no duplicate
inserts when the seed user already exists).

### CorsOptions (`Cors` configuration section)

| Field | Type | Dev value | Local Development value |
|-------|------|-----------|--------------------------|
| `AllowedOrigins` | string[] | Cloudflare Pages Dev origin(s) | `http://localhost:4200`, `https://localhost:4200` |

Replaces **only** the hardcoded origins in `Program.cs`. Authenticated CORS keeps
`AllowCredentials()` and the **explicit** `.WithHeaders("Authorization", "Content-Type", "Accept",
"traceparent", "tracestate", "baggage")` / `.WithMethods("GET","POST","PUT","DELETE","PATCH",
"OPTIONS")` lists introduced by PR #28 (SonarCloud S5122) — `AllowAnyHeader()`/`AllowAnyMethod()`
must **not** be reintroduced.

### Frontend environment (`neko-hoa/src/environments/environment.dev.ts`)

| Field | Type | Dev value |
|-------|------|-----------|
| `production` | bool | `true` (optimized build; not the local dev flag) |
| `apiBaseUrl` | string | `https://api-dev.nekohoa.com/api/v1` |
| `telemetryUrl` | string | `https://api-dev.nekohoa.com/api/v1/telemetry` |
| `propagateTraceHeaderCorsUrls` | string[] | `['https://api-dev.nekohoa.com']` |
| `stripePublishableKey` | string | Stripe **test** publishable key (`pk_test_...`) |

Selected by a new `dev` build configuration in `angular.json` via `fileReplacements`.

---

## Environment matrix (isolation invariant — SC-005)

| Concern | Local Development | **Dev (this feature)** | Staging/Prod |
|---------|-------------------|------------------------|--------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Dev` | `Staging` / `Production` |
| Backend host | Docker Compose | Cloud Run `nekohoa-api-dev` (scale-to-zero) | separate Cloud Run services |
| Database | local Postgres | **Neon Dev** (isolated) | separate Neon DB(s) |
| Object storage | MinIO | Cloudflare **R2 Dev** bucket | separate R2 bucket(s) |
| Frontend | `ng serve` | Cloudflare **Pages Dev** | separate Pages project(s) |
| Edge | none | Cloudflare → `api-dev.nekohoa.com` | Cloudflare → prod domains |
| Swagger | on | on | **off** |
| Migrations at startup | on | on (idempotent) | per env policy |
| Seed data | on | on (idempotent) | off |
| Secrets | `appsettings.Secrets.json` / `.env` | Secret Manager / Cloud Run refs | separate secret sets |
| Stripe | test | test | live (prod) |

**Invariant**: no database connection string, secret value, or storage bucket is shared between
Dev and Staging/Prod.

---

## Pipeline run (logical entity — observed in GitHub Actions, not persisted by the app)

| Attribute | Description |
|-----------|-------------|
| `commit` | `${{ github.sha }}` that triggered the run (merge to `main`) |
| `image` | `sakurapatch/nekohoa-api:${commit}` published by `docker-push` |
| `backend_candidate` | Cloud Run revision deployed `--no-traffic --tag=candidate` |
| `frontend_preview` | Cloudflare Pages preview deployment |
| `gate_health` | `/health` on the candidate returns healthy |
| `gate_e2e` | full E2E suite passes against Dev URLs |
| `promoted` | traffic shifted to candidate + preview promoted to Dev alias (only if gates pass) |
| `status` | success / failure (failure → chat notification) |

**State transitions**:
`triggered → image-resolved → backend-candidate-up → frontend-preview-up → gates(health,e2e) →
[pass] promoted=success | [fail] not-promoted=failure (prior healthy release keeps serving)`.

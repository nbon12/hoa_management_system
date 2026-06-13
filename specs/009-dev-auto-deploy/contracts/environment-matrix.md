# Contract: Dev Environment Configuration & Secrets

**Feature**: 009-dev-auto-deploy

The isolation and configuration contract for the Dev environment. Verifying this contract proves
SC-005 (isolation) and SC-006 (no leaked secrets).

## Backend runtime configuration (Cloud Run `nekohoa-api-dev`)

| Key | Mechanism | Value (Dev) | Isolation rule |
|-----|-----------|-------------|----------------|
| `ASPNETCORE_ENVIRONMENT` | env var | `Dev` | distinct from `Development`/`Staging`/`Production` |
| `Startup__ApplyMigrations` | env / `appsettings.Dev.json` | `true` | — |
| `Startup__SeedData` | env / `appsettings.Dev.json` | `true` | — |
| `Startup__EnableSwagger` | env / `appsettings.Dev.json` | `true` | MUST be `false` in Prod |
| `Cors__AllowedOrigins__0` | env | Cloudflare Pages Dev origin | Dev-only origin |
| `ConnectionStrings__DefaultConnection` | **secret ref** | Neon Dev DB | not shared w/ Staging/Prod |
| `Jwt__Secret` | **secret ref** | Dev-only signing key | distinct per env |
| `Sentry__Dsn` | **secret ref** | Dev Sentry project | distinct per env |
| `Stripe__SecretKey` / `Stripe__WebhookSigningSecret` | **secret ref** | `sk_test_…` / `whsec_…` (test) | test mode only |
| `Storage__ServiceUrl` + `Storage__AccessKey` + `Storage__SecretKey` + `Storage__BucketName` | env + **secret refs** | Cloudflare R2 Dev bucket | not shared w/ Staging/Prod |
| `Jobs__SchedulerSharedSecret` | **secret ref** | Dev-only | distinct per env |
| `Twilio__*` / `SendGrid__*` | **secret ref** (optional) | test creds or unset | alerts disabled if unset |
| `OTEL_EXPORTER_OTLP_ENDPOINT` / `_HEADERS` / `OTEL_SERVICE_NAME` | env + secret | Dev telemetry target | header credential server-side only |

**Rule**: every value above is supplied at deploy/run time. None appears in the repository or in any
container image layer (FR-010, SC-006). Image layers contain only application binaries.

## Frontend configuration (`environment.dev.ts`, build-time)

| Key | Value |
|-----|-------|
| `apiBaseUrl` | `https://api-dev.nekohoa.com/api/v1` |
| `telemetryUrl` | `https://api-dev.nekohoa.com/api/v1/telemetry` |
| `propagateTraceHeaderCorsUrls` | `['https://api-dev.nekohoa.com']` |
| `stripePublishableKey` | `pk_test_…` (test mode; safe to ship to the browser) |

## Edge (Cloudflare)

| Concern | Dev |
|---------|-----|
| API hostname | `api-dev.nekohoa.com` → proxied to Cloud Run `nekohoa-api-dev` |
| Frontend | Cloudflare Pages Dev project (production alias) |
| Rate limiting | app-level limiters (`auth`, `payments`, `telemetry`) active; edge rules consistent with other envs |
| Caching | authenticated responses NOT edge-cached |

## Verification checklist (acceptance)

- [ ] `gcloud run services describe nekohoa-api-dev` shows `ASPNETCORE_ENVIRONMENT=Dev` and secret
      refs (not literal values).
- [ ] Dev DB connection string differs from Staging/Prod (SC-005).
- [ ] `docker history sakurapatch/nekohoa-api:${sha}` / image inspection shows no secret values (SC-006).
- [ ] `https://api-dev.nekohoa.com/health` returns healthy after deploy.
- [ ] `https://api-dev.nekohoa.com/swagger` is reachable in Dev; the Prod equivalent is not.
- [ ] Dev frontend loads and calls `api-dev` (not Staging/Prod).

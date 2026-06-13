# Quickstart: Dev Environment Auto-Deploy

**Feature**: 009-dev-auto-deploy | **Date**: 2026-06-13

How to provision the Dev environment once, and how to verify that merges to `main` auto-deploy to
it. Steps marked **one-time** are platform setup; the day-to-day flow is just "merge to `main`".

## One-time provisioning (platform)

1. **Neon Dev database** — create an isolated Dev database/branch. Note its pooled connection
   string (add `Maximum Pool Size=<low>` for Cloud Run). Enable scale-to-zero.
2. **Cloudflare R2 Dev bucket** — create a Dev documents bucket; create scoped access/secret keys.
3. **Cloud Run service** — create `nekohoa-api-dev` in the project region (scale-to-zero,
   min instances 0). Grant it access to the secrets below.
4. **Google Secret Manager** — create the Dev secrets from
   [`contracts/environment-matrix.md`](./contracts/environment-matrix.md):
   `ConnectionStrings__DefaultConnection`, `Jwt__Secret`, `Sentry__Dsn`, `Stripe__SecretKey`,
   `Stripe__WebhookSigningSecret`, `Storage__*`, `Jobs__SchedulerSharedSecret`, optional
   `Twilio__*`/`SendGrid__*`.
5. **Cloudflare** — create a Dev Pages project; map `api-dev.nekohoa.com` (proxied) to the Cloud
   Run service; confirm authenticated responses are not cached.
6. **GitHub Actions secrets** — add Workload Identity Federation provider + GCP service account,
   `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `DEPLOY_ALERT_WEBHOOK_URL`. (Docker Hub secrets
   already exist.)
7. **Enable the deploy** — the `deploy-dev` job is a **no-op until** the repo variable
   `DEV_DEPLOY_ENABLED` is set to `true`. Set it (and `GCP_REGION`) only after steps 1–6 are done,
   so merges to `main` don't fail against a half-provisioned environment.

### Wiring reference (must match the `deploy-dev` job exactly)

The authored `deploy-dev` job references these names — provision them with these exact identifiers:

| Kind | Name | Value / maps to |
|------|------|-----------------|
| GitHub **variable** | `DEV_DEPLOY_ENABLED` | `true` to enable the job |
| GitHub **variable** | `GCP_REGION` | Cloud Run region (e.g. `us-central1`) |
| GitHub **secret** | `GCP_WIF_PROVIDER` | WIF provider resource name |
| GitHub **secret** | `GCP_DEPLOY_SERVICE_ACCOUNT` | deployer service-account email |
| GitHub **secret** | `CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID` | Pages deploy auth |
| GitHub **secret** | `DEPLOY_ALERT_WEBHOOK_URL` | team-chat webhook (failure alerts) |
| Cloud Run service | `nekohoa-api-dev` | the Dev backend service |
| Cloudflare Pages project | `nekohoa-dev` | the Dev frontend project |
| Secret Manager `dev-db-connection` | → env `ConnectionStrings__DefaultConnection` | Neon Dev DB |
| Secret Manager `dev-jwt-secret` | → env `Jwt__Secret` | Dev JWT signing key |
| Secret Manager `dev-sentry-dsn` | → env `Sentry__Dsn` | Dev Sentry project |
| Secret Manager `dev-stripe-secret-key` / `dev-stripe-webhook-secret` | → `Stripe__SecretKey` / `Stripe__WebhookSigningSecret` | Stripe **test** |
| Secret Manager `dev-storage-service-url` / `dev-storage-access-key` / `dev-storage-secret-key` | → `Storage__ServiceUrl` / `Storage__AccessKey` / `Storage__SecretKey` | R2 Dev bucket |
| Secret Manager `dev-scheduler-secret` | → env `Jobs__SchedulerSharedSecret` | scheduler auth |

(The Secret Manager IDs are wired to env-var names via `--set-secrets` in the job; the env-var
names themselves are the ones in [`contracts/environment-matrix.md`](./contracts/environment-matrix.md).)

## Code changes delivered by this feature

- `HOAManagementCompany/Infrastructure/Configuration/StartupOptions.cs` (new) +
  `Program.cs` edits: migrations/seed/Swagger/CORS become config-driven (see
  [`research.md` D4](./research.md)). Local `docker-compose up` behavior is unchanged.
- `appsettings.json` default `Startup`/`Cors` keys + `appsettings.Dev.json` for the `Dev` env name.
- `neko-hoa/angular.json` `dev` build configuration + `neko-hoa/src/environments/environment.dev.ts`.
- `.github/workflows/test.yml` `deploy-dev` job (see
  [`contracts/pipeline-contract.md`](./contracts/pipeline-contract.md)).

## Verify locally (before relying on the pipeline)

```bash
# Backend builds with the new options and the Dev env name behaves correctly
dotnet build HOAManagementCompany.sln -c Release
dotnet test --filter "FullyQualifiedName~StartupConfig"   # flag-gating tests

# Frontend builds with the new Dev configuration
cd neko-hoa && npm ci && npm run build -- --configuration=dev
```

## Verify the auto-deploy (end-to-end)

1. Open a PR with a small visible change (e.g., a backend response tweak + a UI label), get it
   green, and **merge to `main`**.
2. Watch the **`deploy-dev`** job in the GitHub Actions run. Expect, in order: backend candidate
   deployed (no traffic) → `/health` healthy → frontend preview deployed → E2E suite green →
   promote.
3. Confirm the change is live:
   - `curl https://api-dev.nekohoa.com/health` → healthy.
   - Load the Dev frontend URL → it shows the change and talks to `api-dev`.
4. **Failure path**: merge a change that fails E2E (or a broken migration). Confirm the job fails,
   the **prior** Dev release still serves, and a **chat alert** is posted.

## Success criteria mapping

| Check | Spec criterion |
|-------|----------------|
| Merge triggers deploy, no manual step | SC-001 / FR-001 |
| Change live in Dev ≤ 30 min incl. E2E | SC-002 |
| Failed deploy → prior release keeps serving | SC-003 / FR-007 |
| Migrations auto-applied to Dev DB | SC-004 / FR-004 |
| Seed data present after deploy | SC-009 / FR-004a |
| Dev DB/secrets isolated from Staging/Prod | SC-005 / FR-011 |
| No secrets in repo or image | SC-006 / FR-010 |
| Latest commit wins | SC-007 / FR-009 |
| Status visible + failure alert | SC-008 / FR-008 |
| E2E failure blocks promotion | SC-010 / FR-006 |

# Contract: Conformance to the 009 Environment Matrix

**Feature**: 010-dev-env-iac-opentofu
**Source of truth**: `specs/009-dev-auto-deploy/contracts/environment-matrix.md` and the
`deploy-dev` job in `.github/workflows/test.yml`.

Every value the `009` pipeline hardcodes MUST be produced exactly by this configuration (FR-029,
SC-002). This table is the acceptance checklist — a mismatch breaks the pipeline.

| # | Concern | Contracted value | Produced by |
|---|---------|------------------|-------------|
| 1 | Cloud Run service name | `nekohoa-api-dev` | `google_cloud_run_v2_service.api.name` |
| 2 | Region | = GH var `GCP_REGION` | `var.gcp_region` → output `gcp_region` |
| 3 | Container port | `8080` | `containers.ports.container_port` |
| 4 | Env name | `ASPNETCORE_ENVIRONMENT=Dev` | container env literal |
| 5 | Access | `--allow-unauthenticated` | `run.invoker` → `allUsers` |
| 6 | Health probe | `/health` | startup + liveness probe path |
| 7 | Image (not built here) | `sakurapatch/nekohoa-api:<sha>` | `ignore_changes` on image; pipeline sets it |
| 8 | Secret `dev-db-connection` → `ConnectionStrings__DefaultConnection` | Neon pooled, .NET keyword | auto-wired version |
| 9 | Secret `dev-jwt-secret` → `Jwt__Secret` | operator value | secret + version |
| 10 | Secret `dev-sentry-dsn` → `Sentry__Dsn` | operator value | secret + version |
| 11 | Secret `dev-stripe-secret-key` | `sk_test_…` | secret + version |
| 12 | Secret `dev-stripe-webhook-secret` | `whsec_…` | secret + version |
| 13 | Secret `dev-storage-service-url` | R2 endpoint | secret + version |
| 14 | Secret `dev-storage-access-key` | operator value | secret + version |
| 15 | Secret `dev-storage-secret-key` | operator value | secret + version |
| 16 | Secret `dev-scheduler-secret` | operator value | secret + version |
| 17 | Neon conn format | keyword, pooled, `SSL Mode=Require;Channel Binding=Require` | HCL-assembled string |
| 18 | Pages project | `nekohoa-dev`, branch `main` | `cloudflare_pages_project.frontend` |
| 19 | Frontend domain | `dev.nekohoa.com` | `cloudflare_pages_domain` + record |
| 20 | API domain | `api-dev.nekohoa.com` → `ghs.googlehosted.com`, grey→proxied | domain mapping + record |
| 21 | GH secrets read by job | `GCP_WIF_PROVIDER`, `GCP_DEPLOY_SERVICE_ACCOUNT`, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `DEPLOY_ALERT_WEBHOOK_URL` | outputs (see wiring contract) |
| 22 | GH variables | `GCP_REGION`, `DEV_DEPLOY_ENABLED` | output + manual `true` last |

## Verification procedure

1. `tofu plan` a clean Dev env; confirm planned resource names/values match rows 1–20.
2. `tofu apply`; `gcloud run services describe nekohoa-api-dev` shows rows 1–6 and secret refs (row
   8–16), not literal secret values.
3. `tofu output` covers row 21–22 (cross-check against the wiring contract).
4. Re-`plan`: zero drift.

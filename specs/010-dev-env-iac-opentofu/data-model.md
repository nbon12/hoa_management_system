# Phase 1 Data Model: Provisioned Resources & Module Interface

**Feature**: 010-dev-env-iac-opentofu | **Date**: 2026-06-13

This feature has no application database schema. The "data model" is the **graph of provisioned
cloud resources** and the **module's input/output interface**. All resources are created by the
`environment` module and instantiated per environment.

## Resource inventory (per environment instantiation)

### Neon (provider `kislerdm/neon`)

| Resource | Key attributes | Notes |
|----------|----------------|-------|
| `neon_project` | `name = "nekohoa-${env}"`, `region_id` | one project per env (isolation, §10) |
| `neon_branch` | `name = "dev"`, `project_id` | the environment's branch |
| `neon_database` | `name = "nekohoa"`, `owner_name = role` | |
| `neon_role` | `name`, generated `password` (sensitive) | role/password feed the conn string |
| *(derived)* pooled endpoint host | `*-pooler` host | used to build the .NET keyword string |

### Google Cloud (providers `google` / `google-beta`)

| Resource | Key attributes | FR |
|----------|----------------|----|
| `google_cloud_run_v2_service.api` | name `nekohoa-api-${env_name}`, region var, min-instances 0, port 8080, `ASPNETCORE_ENVIRONMENT=var.aspnet_environment` (Dev), `/health` probes, secret env refs, runtime SA | FR-005/006/008 |
| `google_cloud_run_v2_service_iam_member.public` | `roles/run.invoker` → `allUsers` | unauthenticated |
| `google_cloud_run_domain_mapping.api` | host `api-dev.nekohoa.com` | FR-018 |
| `google_service_account.runtime` | `…-run-dev@` | FR-009 |
| `google_service_account.deployer` | `…-deploy-dev@` | FR-010 |
| `google_secret_manager_secret_iam_member.runtime[*]` | `roles/secretmanager.secretAccessor` per secret → runtime SA | FR-009 |
| `google_project_iam_member.deployer_run_admin` | `roles/run.admin` → deployer SA | FR-010 |
| `google_service_account_iam_member.deployer_sa_user` | `roles/iam.serviceAccountUser` on runtime SA → deployer SA | FR-010 |
| `google_iam_workload_identity_pool.github` | pool id `github-pool` | FR-011 |
| `google_iam_workload_identity_pool_provider.github` | OIDC issuer, attr map, condition `repository=="nbon12/hoa_management_system"` | FR-011/012 |
| `google_service_account_iam_member.wif_deployer` | `roles/iam.workloadIdentityUser` → `principalSet:.../attribute.repository/<repo>` | FR-012 |
| `google_secret_manager_secret.*` (×9) | IDs `${secret_prefix}-…` (Dev → the exact `dev-*` IDs) | FR-013 |
| `google_secret_manager_secret_version.db_connection` | payload = Neon keyword string | FR-014 |
| `google_secret_manager_secret_version.<8 operator secrets>` | payload from tfvars; `ignore_changes=[secret_data]` | FR-015 |

### Cloudflare (provider `cloudflare`)

| Resource | Key attributes | FR |
|----------|----------------|----|
| `cloudflare_pages_project.frontend` | name `nekohoa-dev`, `production_branch="main"` | FR-016 |
| `cloudflare_pages_domain.frontend` | `dev.nekohoa.com` attached to Pages | FR-018 |
| `cloudflare_r2_bucket.documents` | Dev documents bucket | FR-017 |
| `cloudflare_record.frontend` | `dev.nekohoa.com` CNAME → Pages, proxied | FR-018 |
| `cloudflare_record.api` | `api-dev.nekohoa.com` CNAME → `ghs.googlehosted.com`, `proxied=var.api_dns_proxied` | FR-018/019 |

### State backend (GCS) — provisioned by bootstrap

| Resource | Key attributes | FR |
|----------|----------------|----|
| `google_storage_bucket.tfstate` | versioned, UBLA on; backends use `prefix="state/<env>"` | FR-020/031 |
| `google_project_service.*` | enable run/secretmanager/iam/iamcredentials/sts/storage APIs | bootstrap |

## Dependency edges (apply order)

```
bootstrap: APIs enabled → tfstate bucket  (local state, run once)
   ↓ (operator points env backend at the bucket)
neon_project → neon_branch → {neon_database, neon_role}
neon_role(+pooled host) ──┐
                          ├─► secret_version.db_connection ─► secret.dev-db-connection
runtime SA ───────────────┴─► cloud_run_v2_service (secret env refs) ─► domain_mapping ─► cloudflare_record.api
deployer SA ─► {run.admin, iam.serviceAccountUser(runtime SA)}
WIF pool → WIF provider → wif_deployer binding (deployer SA)
cloudflare: pages_project → pages_domain(dev.nekohoa.com); r2_bucket; record.frontend
```

## Module interface (`infra/modules/environment`)

### Inputs (`variables.tf`)

| Variable | Type | Example (Dev) | Sensitive |
|----------|------|---------------|-----------|
| `env_name` | string | `"dev"` | no |
| `aspnet_environment` | string | `"Dev"` (maps dev→Dev, staging→Staging, prod→**Production**) | no |
| `secret_prefix` | string | `"dev"` (Dev resolves the literal `dev-*` IDs; FR-013/029) | no |
| `gcp_project_id` | string | `"nekohoa"` | no |
| `gcp_region` | string | `"us-central1"` | no |
| `github_repository` | string | `"nbon12/hoa_management_system"` | no |
| `frontend_domain` | string | `"dev.nekohoa.com"` | no |
| `api_domain` | string | `"api-dev.nekohoa.com"` | no |
| `api_dns_proxied` | bool | `false` → `true` (step 2) | no |
| `container_image` | string | `"sakurapatch/nekohoa-api:latest"` (placeholder; ignored after) | no |
| `neon_region_id` | string | `"aws-us-east-1"` | no |
| `neon_api_key` | string | — | **yes** |
| `cloudflare_api_token` | string | — | **yes** |
| `cloudflare_account_id` | string | — | no |
| `cloudflare_zone_id` | string | — | no |
| `operator_secrets` | map(string) | jwt/sentry/stripe/storage/scheduler values | **yes** |
| `deploy_alert_webhook_url` | string | — | **yes** |

### Outputs (`outputs.tf`) — drive `quickstart.md` and the wiring contract

| Output | Sensitive | Maps to GitHub |
|--------|-----------|----------------|
| `db_connection_string` | **yes** | (internal — auto-wired into `dev-db-connection`) |
| `wif_provider` | no | secret `GCP_WIF_PROVIDER` |
| `deployer_service_account` | no | secret `GCP_DEPLOY_SERVICE_ACCOUNT` |
| `gcp_region` | no | variable `GCP_REGION` |
| `cloudflare_account_id` | no | secret `CLOUDFLARE_ACCOUNT_ID` |
| `cloudflare_api_token` | **yes** | secret `CLOUDFLARE_API_TOKEN` (echoed input) |
| `deploy_alert_webhook_url` | **yes** | secret `DEPLOY_ALERT_WEBHOOK_URL` (echoed input) |
| `cloud_run_service_url` | no | sanity check / health probe target |
| `next_steps` | no | human checklist ending with "set `DEV_DEPLOY_ENABLED=true` **last**" |

## Validation / invariants

- `container_image` change is **ignored** after first apply (FR-007).
- `secret_id` = `"${var.secret_prefix}-…"`; with `secret_prefix = "dev"` they equal the literal
  matrix IDs exactly (FR-013/029) — the implement step asserts this.
- `aspnet_environment` for Dev is `"Dev"`; for a future Prod it MUST be `"Production"` (not `"prod"`)
  and Staging `"Staging"`.
- Neon project/branch are created **per environment instantiation**, so the Dev database is distinct
  from any Staging/Prod database (FR-004).
- `db_connection_string` matches `^Host=.*;SSL Mode=Require;Channel Binding=Require$` and contains no
  `postgresql://` (FR-003).
- WIF attribute condition equals `assertion.repository == "nbon12/hoa_management_system"` (FR-012).
- Sensitive variables/outputs never appear in committed files (SC-005).
- Re-`plan` after `apply` ⇒ no changes (SC-003).

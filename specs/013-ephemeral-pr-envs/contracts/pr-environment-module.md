# Contract: `modules/pr-environment` OpenTofu module

The per-PR provisioning unit. Instantiated by `infra/environments/pr/` once per PR.
Creates only per-PR resources (D1); references shared primitives by id.

## Inputs (variables)

| Variable | Type | Required | Notes |
|----------|------|----------|-------|
| `pr_number` | number | yes | Identity; drives all names, labels, `secret_prefix=pr-<n>` |
| `head_sha` | string | yes | Selects the image tag `sakurapatch/nekohoa-api:pr-<n>-<sha>` |
| `gcp_project_id` | string | yes | Shared Dev project |
| `gcp_region` | string | yes | Default `us-central1` |
| `neon_project_id` | string | yes | Shared Neon project `super-water-18090867` |
| `neon_base_branch` | string | yes | `pr-base` — fork source (pre-seeded) |
| `neon_api_key` | string (sensitive) | yes | Shared |
| `cloudflare_account_id` | string | yes | Shared |
| `cloudflare_api_token` | string (sensitive) | yes | Shared |
| `runtime_service_account` | string | yes | Shared `nekohoa-run-dev` SA email |
| `shared_secret_ids` | map(string) | yes | Secret Manager ids for the 8 operator secrets |
| `stripe_publishable_key` | string | yes | Test mode, non-secret env var |
| `labels` | map(string) | no | Merged with `{pr-env="true", pr-number=tostring(pr_number)}` |

## Outputs

| Output | Type | Consumed by |
|--------|------|-------------|
| `api_url` | string | Frontend build (API base), Stripe webhook target, smoke tests |
| `web_branch` | string | `pr-<n>` Pages branch → `pr-<n>.nekohoa-dev.pages.dev` |
| `neon_branch_id` | string | Diagnostics / sweep |
| `db_connection_secret_id` | string | Cloud Run secret wiring |
| `stripe_webhook_secret_id` | string | Stripe webhook script writes the signing secret here |
| `r2_bucket_name` | string | Cloud Run `Storage__BucketName` env var |

## Guarantees

- All created GCP resources carry labels `pr-env=true`, `pr-number=<n>` (FR-014).
- Names are a pure function of `pr_number` ⇒ no cross-PR collision (FR-002).
- `tofu destroy` with this PR's state prefix removes every created resource (FR-006/008).
- No production resource is referenced (FR-011).
- Cloud Run runs `ASPNETCORE_ENVIRONMENT=Dev`, scale-to-zero, public invoker.

## State backend

Set at init, not in the module:
`tofu -chdir=infra/environments/pr init -backend-config="prefix=state/pr/${PR_NUMBER}"`
(bucket `nekohoa-dev-tfstate` static in `backend.tf`).

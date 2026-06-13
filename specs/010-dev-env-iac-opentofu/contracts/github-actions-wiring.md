# Contract: OpenTofu Outputs → GitHub Actions Secrets/Variables

**Feature**: 010-dev-env-iac-opentofu

After `tofu apply` for the Dev environment, the operator wires the `009` pipeline using **only**
these outputs (FR-023, SC-006). The configuration prints values; the operator sets them in GitHub
(this config never writes GitHub secrets itself).

## Repository secrets (Settings → Secrets and variables → Actions → Secrets)

| GitHub secret | Source output | Sensitive | Notes |
|---------------|---------------|-----------|-------|
| `GCP_WIF_PROVIDER` | `wif_provider` | no (id only) | full provider resource name `projects/<n>/locations/global/workloadIdentityPools/<pool>/providers/<provider>` |
| `GCP_DEPLOY_SERVICE_ACCOUNT` | `deployer_service_account` | no | deployer SA email |
| `CLOUDFLARE_API_TOKEN` | `cloudflare_api_token` | **yes** | echoed operator input |
| `CLOUDFLARE_ACCOUNT_ID` | `cloudflare_account_id` | no | |
| `DEPLOY_ALERT_WEBHOOK_URL` | `deploy_alert_webhook_url` | **yes** | echoed operator input |

## Repository variables (… → Variables)

| GitHub variable | Source output | Set when |
|-----------------|---------------|----------|
| `GCP_REGION` | `gcp_region` | with the secrets above |
| `DEV_DEPLOY_ENABLED` | (none — literal `true`) | **LAST**, after everything else verifies |

## Ordering rule (enforced by the `next_steps` output)

1. Set all five secrets + `GCP_REGION`.
2. Confirm `tofu output` shows no missing/empty values and the Cloud Run service + custom domain are
   healthy (`https://api-dev.nekohoa.com/health`).
3. **Only then** set `DEV_DEPLOY_ENABLED=true` — this is the switch that lets the 009 `deploy-dev`
   job run on the next merge to `main`.

## Verification

- [ ] Each secret/variable above has a corresponding non-empty `tofu output`.
- [ ] Sensitive outputs are masked in `tofu apply` summary (shown as `<sensitive>`).
- [ ] The 009 `deploy-dev` job's referenced secret/variable names (in `.github/workflows/test.yml`)
      exactly match the names in this table.

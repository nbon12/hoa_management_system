# <!-- REPOWISE:START section=env-module-outputs -->
# Published outputs of the `environment` module. They drive the operator runbook (quickstart.md) and
# the GitHub Actions wiring contract: after apply, `tofu output` prints every secret/variable the 009
# deploy job needs. Sensitive outputs (db connection, echoed tokens, webhook) are marked `sensitive`
# so they never appear in the apply summary or PR plan in plaintext (FR-023/024, SC-005/006).
#
# | Output                     | Sensitive | GitHub target                                   |
# |----------------------------|-----------|-------------------------------------------------|
# | db_connection_string       | yes       | (internal — auto-wired into dev-db-connection)  |
# | wif_provider               | no        | secret  GCP_WIF_PROVIDER                         |
# | deployer_service_account   | no        | secret  GCP_DEPLOY_SERVICE_ACCOUNT              |
# | gcp_region                 | no        | variable GCP_REGION                             |
# | cloudflare_account_id      | no        | secret  CLOUDFLARE_ACCOUNT_ID                   |
# | cloudflare_api_token       | yes       | secret  CLOUDFLARE_API_TOKEN (echoed input)     |
# | deploy_alert_webhook_url   | yes       | secret  DEPLOY_ALERT_WEBHOOK_URL (echoed input) |
# | cloud_run_service_url      | no        | sanity / health-probe target                    |
# | next_steps                 | no        | human checklist (ends: set DEV_DEPLOY_ENABLED last) |
# <!-- REPOWISE:END -->

output "db_connection_string" {
  description = "Neon pooled .NET keyword connection string (auto-wired into the db-connection secret)."
  value       = local.db_connection_string
  sensitive   = true
}

output "wif_provider" {
  description = "Full WIF provider resource name → GitHub secret GCP_WIF_PROVIDER."
  value       = google_iam_workload_identity_pool_provider.github.name
}

output "deployer_service_account" {
  description = "Deployer SA email → GitHub secret GCP_DEPLOY_SERVICE_ACCOUNT."
  value       = google_service_account.deployer.email
}

output "gcp_region" {
  description = "Cloud Run region → GitHub variable GCP_REGION."
  value       = var.gcp_region
}

output "cloudflare_account_id" {
  description = "Cloudflare account id → GitHub secret CLOUDFLARE_ACCOUNT_ID."
  value       = var.cloudflare_account_id
}

output "cloudflare_api_token" {
  description = "Echoed operator input → GitHub secret CLOUDFLARE_API_TOKEN."
  value       = var.cloudflare_api_token
  sensitive   = true
}

output "deploy_alert_webhook_url" {
  description = "Echoed operator input → GitHub secret DEPLOY_ALERT_WEBHOOK_URL."
  value       = var.deploy_alert_webhook_url
  sensitive   = true
}

output "cloud_run_service_url" {
  description = "Direct Cloud Run URL (sanity check / health probe before the custom domain is live)."
  value       = module.api_service.uri
}

output "next_steps" {
  description = "Operator checklist for wiring GitHub Actions. Ends with DEV_DEPLOY_ENABLED=true LAST."
  value       = <<-EOT
    Wire the 009 pipeline from these outputs (see contracts/github-actions-wiring.md):

    Repository SECRETS (Settings → Secrets and variables → Actions → Secrets):
      GCP_WIF_PROVIDER          = <output wif_provider>
      GCP_DEPLOY_SERVICE_ACCOUNT= <output deployer_service_account>
      CLOUDFLARE_API_TOKEN      = <output cloudflare_api_token>     (sensitive — view individually)
      CLOUDFLARE_ACCOUNT_ID     = <output cloudflare_account_id>
      DEPLOY_ALERT_WEBHOOK_URL  = <output deploy_alert_webhook_url> (sensitive — view individually)

    Repository VARIABLE:
      GCP_REGION                = <output gcp_region>

    Then verify https://${var.api_domain}/health is healthy.

    FINALLY, and only after everything above verifies, set the repository variable:
      DEV_DEPLOY_ENABLED = true        ← set this LAST; it is the switch that lets 009 deploy on merge.
  EOT
}

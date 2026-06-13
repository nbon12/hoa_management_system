# Re-export module outputs unchanged (same as Dev).

output "db_connection_string" {
  value     = module.environment.db_connection_string
  sensitive = true
}

output "wif_provider" {
  value = module.environment.wif_provider
}

output "deployer_service_account" {
  value = module.environment.deployer_service_account
}

output "gcp_region" {
  value = module.environment.gcp_region
}

output "cloudflare_account_id" {
  value = module.environment.cloudflare_account_id
}

output "cloudflare_api_token" {
  value     = module.environment.cloudflare_api_token
  sensitive = true
}

output "deploy_alert_webhook_url" {
  value     = module.environment.deploy_alert_webhook_url
  sensitive = true
}

output "cloud_run_service_url" {
  value = module.environment.cloud_run_service_url
}

output "next_steps" {
  value = module.environment.next_steps
}

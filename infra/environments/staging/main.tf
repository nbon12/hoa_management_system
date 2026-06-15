# Staging instantiation of the SAME module as Dev — the only differences are inputs (SC-009). Note:
# aspnet_environment = "Staging" (NOT title(env_name) tricks; this maps staging → "Staging"), and
# secret_prefix = "staging" so secret IDs become staging-* without any module change.

provider "google" {
  project = var.gcp_project_id
  region  = var.gcp_region
}

provider "google-beta" {
  project = var.gcp_project_id
  region  = var.gcp_region
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

provider "neon" {
  api_key = var.neon_api_key
}

module "environment" {
  source = "../../modules/environment"

  env_name           = "staging"
  aspnet_environment = "Staging"
  secret_prefix      = "staging"
  state_bucket       = "nekohoa-dev-tfstate" # matches backend.tf (shared state bucket, per-env prefix)



  gcp_project_id    = var.gcp_project_id
  gcp_region        = var.gcp_region
  github_repository = var.github_repository

  frontend_domain   = "staging.nekohoa.com"
  api_domain        = "api-staging.nekohoa.com"
  enable_api_domain = var.enable_api_domain
  api_dns_proxied   = var.api_dns_proxied
  container_image   = var.container_image

  stripe_publishable_key = var.stripe_publishable_key

  neon_region_id = var.neon_region_id
  neon_api_key   = var.neon_api_key

  cloudflare_api_token  = var.cloudflare_api_token
  cloudflare_account_id = var.cloudflare_account_id
  cloudflare_zone_id    = var.cloudflare_zone_id

  operator_secrets         = var.operator_secrets
  deploy_alert_webhook_url = var.deploy_alert_webhook_url
}

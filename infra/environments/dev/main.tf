# Provider configurations for the Dev root. Credentials come from variables / TF_VAR_* — NO static
# keys are committed (FR-027). GCP auth is operator ADC locally, or WIF in CI.

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

# The single environment definition, instantiated for Dev. env_name="dev", aspnet_environment="Dev"
# (NOT title(env_name)), secret_prefix="dev" so secret IDs resolve to the literal dev-* (FR-013/030).
module "environment" {
  source = "../../modules/environment"

  env_name           = "dev"
  aspnet_environment = "Dev"
  secret_prefix      = "dev"
  state_bucket       = "nekohoa-dev-tfstate" # matches backend.tf



  gcp_project_id    = var.gcp_project_id
  gcp_region        = var.gcp_region
  github_repository = var.github_repository

  frontend_domain   = "dev.nekohoa.com"
  api_domain        = "api-dev.nekohoa.com"
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

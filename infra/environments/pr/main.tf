# Per-PR environment root. Providers are configured here (credentials from TF_VAR_* — no static keys,
# 013 FR-016). One root instantiates the pr-environment module for whichever PR `pr_number` selects.

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

module "pr_environment" {
  source = "../../modules/pr-environment"

  pr_number = var.pr_number
  head_sha  = var.head_sha

  gcp_project_id = var.gcp_project_id
  gcp_region     = var.gcp_region

  neon_project_id     = var.neon_project_id
  neon_base_branch_id = var.neon_base_branch_id
  neon_role_password  = var.neon_role_password

  runtime_service_account = var.runtime_service_account
  shared_secret_prefix    = var.shared_secret_prefix
  stripe_publishable_key  = var.stripe_publishable_key
  cloudflare_account_id   = var.cloudflare_account_id

  # Cost-attribution + sweep labels (research D1, FR-014).
  labels = {
    "pr-env"    = "true"
    "pr-number" = tostring(var.pr_number)
  }
}

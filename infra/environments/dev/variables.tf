# Dev environment inputs. Secret-bearing values come from a gitignored terraform.tfvars (or TF_VAR_*
# in CI) — never committed. terraform.tfvars.example documents the shape.

variable "gcp_project_id" {
  type = string
}

variable "gcp_region" {
  type    = string
  default = "us-central1"
}

variable "github_repository" {
  type    = string
  default = "nbon12/hoa_management_system"
}

variable "enable_api_domain" {
  description = "Create the api-dev.nekohoa.com custom-domain mapping + record (needs Google domain verification first)."
  type        = bool
  default     = true
}

variable "api_dns_proxied" {
  description = "Proxied Full(strict) for the API custom domain. Default true now that the managed cert is issued; CI keeps it proxied. (Set false only to re-issue/replace the cert via grey-cloud.)"
  type        = bool
  default     = true
}

variable "container_image" {
  description = "Placeholder image for the first create only; ignored thereafter (FR-007)."
  type        = string
  default     = "sakurapatch/nekohoa-api:latest"
}

variable "stripe_publishable_key" {
  description = "Stripe publishable key (pk_test_… for Dev). Non-secret; set as a Cloud Run env var."
  type        = string
}

variable "neon_region_id" {
  type    = string
  default = "aws-us-east-1"
}

variable "neon_api_key" {
  type      = string
  sensitive = true
}

variable "cloudflare_api_token" {
  type      = string
  sensitive = true
}

variable "cloudflare_account_id" {
  type = string
}

variable "cloudflare_zone_id" {
  type = string
}

variable "operator_secrets" {
  description = "The eight operator secret values (see module variables.tf for required keys)."
  type        = map(string)
  sensitive   = true
}

variable "deploy_alert_webhook_url" {
  type      = string
  sensitive = true
}

# --- 013: PR-environment cost guardrail (budget.tf, SC-008) ---

variable "billing_account_id" {
  description = "GCP billing account id (XXXXXX-XXXXXX-XXXXXX) the PR-env budget is created under. From TF_VAR_billing_account_id / GH secret."
  type        = string
}

variable "pr_env_monthly_budget" {
  description = "Monthly USD ceiling for all per-PR environments combined; alerts fire at 80% and 100% (SC-008). Alert-only — no hard cap."
  type        = number
  default     = 25
}

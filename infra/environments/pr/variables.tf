# Per-PR environment inputs. The PR-specific values (pr_number, head_sha) are passed by the
# pr-env.yml workflow via TF_VAR_*; the shared/credential values come from TF_VAR_* sourced from the
# required-reviewer GitHub Environment. Never committed (infra/.gitignore).

variable "pr_number" {
  description = "Pull-request number (github.event.number)."
  type        = number
}

variable "head_sha" {
  description = "PR head commit SHA (github.event.pull_request.head.sha)."
  type        = string
}

variable "gcp_project_id" {
  type = string
}

variable "gcp_region" {
  type    = string
  default = "us-central1"
}

variable "neon_project_id" {
  description = "Shared Neon project id (super-water-18090867)."
  type        = string
}

variable "neon_base_branch_id" {
  description = "Branch id of the pre-seeded pr-base branch to fork."
  type        = string
}

variable "neon_role_password" {
  description = "Password of the inherited nekohoa_app role on pr-base (identical on every fork)."
  type        = string
  sensitive   = true
}

variable "runtime_service_account" {
  description = "Shared runtime SA email (nekohoa-run-dev@...)."
  type        = string
}

variable "shared_secret_prefix" {
  type    = string
  default = "dev"
}

variable "stripe_publishable_key" {
  description = "Stripe TEST publishable key (pk_test_…)."
  type        = string
}

variable "cloudflare_api_token" {
  description = "Cloudflare API token scoped to R2 edit. Configures the provider."
  type        = string
  sensitive   = true
}

variable "cloudflare_account_id" {
  type = string
}

variable "neon_api_key" {
  description = "Neon API key. Configures the provider."
  type        = string
  sensitive   = true
}

# Input surface for the lightweight per-PR module (013 contracts/pr-environment-module.md).
# Creates ONLY the cheap per-PR resources; the GCP project, Neon PROJECT, WIF, service accounts, and
# the 7 shared operator secrets are referenced by id, never recreated (research D1).

variable "pr_number" {
  description = "Pull-request number. Identity for all per-PR resource names, labels, and the secret prefix pr-<n>."
  type        = number
}

variable "head_sha" {
  description = "PR head commit SHA. Selects the image tag sakurapatch/nekohoa-api:pr-<n>-<sha> (pipeline-owned thereafter)."
  type        = string
}

variable "gcp_project_id" {
  description = "Shared Dev GCP project hosting Cloud Run + Secret Manager."
  type        = string
}

variable "gcp_region" {
  description = "GCP region for the per-PR Cloud Run service."
  type        = string
}

variable "neon_project_id" {
  description = "Existing SHARED Neon project id (super-water-18090867). The per-PR branch forks inside it; the project is never recreated (research D1/D3)."
  type        = string
}

variable "neon_base_branch_id" {
  description = "Branch id of the long-lived, pre-seeded `pr-base` branch. The per-PR branch is a copy-on-write fork of it (research D3)."
  type        = string
}

# A forked Neon branch INHERITS the parent branch's roles and databases (same names + same passwords);
# recreating them would conflict. So the role/database are referenced by name and the inherited
# password is passed in (set once when pr-base is created). Only a new compute endpoint is created.
variable "neon_role_name" {
  description = "App role inherited from pr-base (default nekohoa_app)."
  type        = string
  default     = "nekohoa_app"
}

variable "neon_database_name" {
  description = "App database inherited from pr-base (default nekohoa)."
  type        = string
  default     = "nekohoa"
}

variable "neon_role_password" {
  description = "Password of the inherited app role on pr-base (identical on every fork). Sourced from the required-reviewer GitHub Environment; never committed."
  type        = string
  sensitive   = true
}

variable "runtime_service_account" {
  description = "Email of the SHARED runtime SA (nekohoa-run-dev) the per-PR Cloud Run service runs as. Already a secret accessor on dev-* secrets; granted accessor on the per-PR secrets here."
  type        = string
}

variable "shared_secret_prefix" {
  description = "Prefix of the SHARED operator secrets reused from Dev (e.g. \"dev\" → dev-jwt-secret). The 7 shared secrets are referenced, not recreated; only db-connection and stripe-webhook-secret are per-PR."
  type        = string
  default     = "dev"
}

variable "stripe_publishable_key" {
  description = "Stripe TEST publishable key (pk_test_…). Non-secret; a plain Cloud Run env var. Shared with Dev."
  type        = string
}

variable "container_image" {
  description = "Placeholder image for the FIRST create only. The pr-env.yml workflow deploys the real sakurapatch/nekohoa-api:pr-<n>-<sha> image-only via gcloud AFTER the Stripe webhook secret is written; ignore_changes keeps tofu from reverting it (mirrors modules/environment)."
  type        = string
  default     = "sakurapatch/nekohoa-api:latest"
}

variable "cloudflare_account_id" {
  description = "Cloudflare account id (for the per-PR R2 bucket)."
  type        = string
}

variable "labels" {
  description = "Labels applied to GCP resources for cost attribution + the sweep. The caller merges { pr-env = \"true\", pr-number = tostring(pr_number) }."
  type        = map(string)
  default     = {}
}

# Shared Cloud Run API service core (015 US6, FR-019) — the single definition previously
# maintained as ~150-line near-twins in modules/environment and modules/pr-environment.
# Callers express their variant through these inputs.

variable "gcp_project_id" {
  description = "GCP project hosting the service."
  type        = string
}

variable "gcp_region" {
  description = "Cloud Run region."
  type        = string
}

variable "service_name" {
  description = "Full service name (nekohoa-api-<env> or nekohoa-api-pr-<n>)."
  type        = string
}

variable "labels" {
  description = "Service labels (PR envs carry TTL/sweep labels; long-lived envs none)."
  type        = map(string)
  default     = {}
}

variable "runtime_service_account" {
  description = "Email of the runtime service account the revision runs as."
  type        = string
}

variable "container_image" {
  description = "Initial container image (pipeline-owned afterwards; ignore_changes applies)."
  type        = string
}

variable "aspnet_environment" {
  description = "ASPNETCORE_ENVIRONMENT literal (config-validation known set: Dev, Staging, Production)."
  type        = string
}

variable "stripe_publishable_key" {
  description = "Stripe publishable key (pk_…) — browser-safe, plain env var."
  type        = string
}

variable "documents_bucket_name" {
  description = "R2 documents bucket for this environment (path-style addressing required by R2)."
  type        = string
}

variable "secret_env_refs" {
  description = <<-EOT
    .NET env key → Secret Manager secret id/name, each wired to its latest version. Callers build
    the full map: the long-lived environment passes its nine module-created secrets; the PR
    environment passes seven shared dev-* names plus its two per-PR secret ids.
  EOT
  type        = map(string)
}

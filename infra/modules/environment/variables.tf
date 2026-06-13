# Input surface for the reusable `environment` module (contracts/module-interface.md, data-model.md).
# NOTHING in this module hardcodes an environment name — every name derives from these inputs, so
# Staging/Prod reuse the module with zero edits (FR-030, SC-009).

variable "env_name" {
  description = "Short environment slug used to derive resource names (e.g. nekohoa-api-$${env_name}). Dev passes \"dev\"."
  type        = string
}

variable "aspnet_environment" {
  description = <<-EOT
    Value for the runtime ASPNETCORE_ENVIRONMENT. This is a DISTINCT input, NOT title(env_name):
    map dev → "Dev", staging → "Staging", prod → "Production". Passing title(env_name) would wrongly
    yield "Prod" for production. Dev passes "Dev" (FR-006, FR-030, matrix row 4).
  EOT
  type        = string
}

variable "secret_prefix" {
  description = <<-EOT
    Prefix for Secret Manager secret IDs. Secret IDs are LITERAL per the 009 contract, so Dev passes
    "dev" to resolve the exact dev-* IDs (e.g. dev-db-connection). Kept as an explicit input so the
    literal-vs-derived boundary is visible for Staging/Prod (FR-013/FR-029).
  EOT
  type        = string
}

variable "gcp_project_id" {
  description = "GCP project hosting Cloud Run, Secret Manager, IAM, and WIF."
  type        = string
}

variable "gcp_region" {
  description = "GCP region for the Cloud Run service. Surfaced as the gcp_region output → GH var GCP_REGION (matrix row 2)."
  type        = string
}

variable "github_repository" {
  description = "owner/repo used VERBATIM in the WIF attribute condition (assertion.repository == this). Repo-scoped, not ref-scoped (FR-012)."
  type        = string
}

variable "frontend_domain" {
  description = "Custom domain for the Cloudflare Pages frontend (Dev: dev.nekohoa.com)."
  type        = string
}

variable "api_domain" {
  description = "Custom domain for the Cloud Run API (Dev: api-dev.nekohoa.com)."
  type        = string
}

variable "api_dns_proxied" {
  description = <<-EOT
    Cloudflare orange/grey cloud for the API record. Two-step cert flow: apply with `false`
    (grey-cloud) so Google issues the Cloud Run domain-mapping cert, then set `true` and re-apply for
    proxied Full(strict) (FR-019, quickstart Steps 3-4).
  EOT
  type        = bool
  default     = false
}

variable "container_image" {
  description = <<-EOT
    Placeholder image for the FIRST-EVER service create only. After the 009 pipeline pushes a real
    :sha, lifecycle.ignore_changes keeps infra from reverting it (FR-007).
  EOT
  type        = string
  default     = "sakurapatch/nekohoa-api:latest"
}

variable "stripe_publishable_key" {
  description = <<-EOT
    Stripe publishable key (pk_test_… for Dev). NON-secret (browser-safe), so it is set as a plain
    Cloud Run env var Stripe__PublishableKey, not a Secret Manager entry. The backend requires it at
    startup (StripeOptionsValidator); base appsettings documents that deployed envs supply the Stripe
    values via env vars. SecretKey/WebhookSigningSecret remain in Secret Manager.
  EOT
  type        = string
}

variable "neon_region_id" {
  description = "Neon region id for the project (e.g. aws-us-east-1)."
  type        = string
}

variable "neon_api_key" {
  description = "Neon API key. Supplied via gitignored tfvars / TF_VAR_neon_api_key — never committed."
  type        = string
  sensitive   = true
}

variable "cloudflare_api_token" {
  description = "Cloudflare API token scoped to Pages, R2, and DNS edit for the zone. Echoed back as a sensitive output for GH wiring."
  type        = string
  sensitive   = true
}

variable "cloudflare_account_id" {
  description = "Cloudflare account id (Pages/R2). Surfaced as a non-sensitive output → GH secret CLOUDFLARE_ACCOUNT_ID."
  type        = string
}

variable "cloudflare_zone_id" {
  description = "Cloudflare zone id for nekohoa.com (DNS records)."
  type        = string
}

variable "operator_secrets" {
  description = <<-EOT
    The eight operator-supplied secret values keyed by their unprefixed secret name. Each becomes the
    first version of `$${secret_prefix}-<key>` with ignore_changes on the body so operators can rotate
    out-of-band. Expected keys (matrix rows 9-16):
      jwt-secret, sentry-dsn, stripe-secret-key, stripe-webhook-secret,
      storage-service-url, storage-access-key, storage-secret-key, scheduler-secret.
    (db-connection is the 9th secret and is auto-wired from Neon, NOT supplied here.)
  EOT
  type        = map(string)
  sensitive   = true

  validation {
    condition = length(setsubtract([
      "jwt-secret", "sentry-dsn", "stripe-secret-key", "stripe-webhook-secret",
      "storage-service-url", "storage-access-key", "storage-secret-key", "scheduler-secret",
    ], keys(var.operator_secrets))) == 0
    error_message = "operator_secrets must contain all eight keys: jwt-secret, sentry-dsn, stripe-secret-key, stripe-webhook-secret, storage-service-url, storage-access-key, storage-secret-key, scheduler-secret."
  }
}

variable "deploy_alert_webhook_url" {
  description = "Webhook URL the 009 deploy job posts alerts to. Echoed back as a sensitive output → GH secret DEPLOY_ALERT_WEBHOOK_URL."
  type        = string
  sensitive   = true
}

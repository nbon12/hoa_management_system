# Staging inputs (same shape as Dev). Secret-bearing values come from a gitignored terraform.tfvars
# or TF_VAR_* in CI.

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

variable "api_dns_proxied" {
  type    = bool
  default = false
}

variable "container_image" {
  type    = string
  default = "sakurapatch/nekohoa-api:latest"
}

variable "stripe_publishable_key" {
  type = string
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
  type      = map(string)
  sensitive = true
}

variable "deploy_alert_webhook_url" {
  type      = string
  sensitive = true
}

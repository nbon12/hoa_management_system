terraform {
  required_version = ">= 1.8.0"

  # Same state bucket as Dev, isolated by prefix (FR-020). Proof that adding an environment needs only
  # a new directory with its own backend prefix + tfvars — zero edits to modules/environment (SC-009).
  backend "gcs" {
    bucket = "REPLACE_WITH_STATE_BUCKET_FROM_BOOTSTRAP"
    prefix = "state/staging"
  }

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = "~> 5.0"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
    neon = {
      source  = "kislerdm/neon"
      version = "= 0.6.3"
    }
  }
}

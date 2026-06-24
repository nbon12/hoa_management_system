terraform {
  # OpenTofu (Terraform-compatible). Same floor as modules/environment so the Cloud Run v2 /
  # Secret Manager / billing-budget / R2 resource shapes are present (013 FR-016).
  required_version = ">= 1.8.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = "~> 5.0"
    }
    # Cloudflare v4 spelling (cloudflare_r2_bucket) — mirrors modules/environment/versions.tf.
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
    # ⚠️ COMMUNITY-MAINTAINED kislerdm/neon, exact pin — see modules/environment/versions.tf.
    neon = {
      source  = "kislerdm/neon"
      version = "= 0.6.3"
    }
  }
}

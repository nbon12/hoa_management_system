terraform {
  # OpenTofu (Terraform-compatible). Pinned floor so probe/secret/WIF features are present.
  required_version = ">= 1.8.0"

  required_providers {
    # HashiCorp-maintained. ~> 5.0 keeps the Cloud Run v2 / Secret Manager / WIF / GCS
    # resource shapes used in this module (FR-021).
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    # google-beta is needed for some Cloud Run v2 + WIF surface (research §1).
    google-beta = {
      source  = "hashicorp/google-beta"
      version = "~> 5.0"
    }
    # Cloudflare provider v4 — the Pages/R2/DNS resource names used here
    # (cloudflare_pages_project / cloudflare_pages_domain / cloudflare_r2_bucket /
    # cloudflare_record) are the v4 spelling; v5 renamed several of them. Pin to 4.x (FR-021).
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
    # ⚠️ COMMUNITY-MAINTAINED — NOT HashiCorp/OpenTofu-verified. The only viable Neon provider.
    # Exact pin (not a range) mitigates the supply-chain/stability risk of a community provider
    # (FR-021, research §2). Bump deliberately after reviewing the changelog.
    neon = {
      source  = "kislerdm/neon"
      version = "= 0.6.3"
    }
  }
}

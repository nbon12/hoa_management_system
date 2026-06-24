terraform {
  required_version = ">= 1.8.0"

  # Per-PR remote state lives in the SAME versioned bucket as Dev/Staging, isolated by a per-PR
  # `prefix`. The prefix is NOT hardcoded here — it is supplied at init time so one root serves every
  # PR (research D2):
  #   tofu -chdir=infra/environments/pr init -backend-config="prefix=state/pr/${PR_NUMBER}"
  backend "gcs" {
    bucket = "nekohoa-dev-tfstate"
    # prefix is injected via -backend-config (state/pr/<n>); do not set it here.
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

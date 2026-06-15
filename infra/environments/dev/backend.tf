terraform {
  required_version = ">= 1.8.0"

  # Remote state in the versioned bucket created by infra/bootstrap/state-bucket. Each environment is
  # isolated by `prefix` (FR-020). Fill `bucket` with the bootstrap's output (see its README).
  backend "gcs" {
    bucket = "nekohoa-dev-tfstate"
    prefix = "state/dev"
  }

  # Root must declare the providers it configures below; pins mirror modules/environment/versions.tf.
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
    # COMMUNITY-MAINTAINED — see modules/environment/versions.tf (FR-021).
    neon = {
      source  = "kislerdm/neon"
      version = "= 0.6.3"
    }
  }
}

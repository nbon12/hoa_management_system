# One-time bootstrap (FR-031). Uses LOCAL state — there is no backend block — to resolve the
# chicken-and-egg of "the remote state backend needs a bucket that doesn't exist yet". Run once with
# operator ADC (`gcloud auth application-default login`); thereafter every environment uses the GCS
# backend keyed by `prefix = "state/<env>"`.

terraform {
  required_version = ">= 1.8.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

provider "google" {
  project = var.gcp_project_id
  region  = var.gcp_region
}

# Enable every GCP API the environment module needs, so the first env apply doesn't fail on a
# not-yet-enabled service (FR-020). disable_on_destroy = false so tearing down the bootstrap never
# yanks APIs out from under a live environment.
resource "google_project_service" "required" {
  for_each = toset([
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "sts.googleapis.com",
    "storage.googleapis.com",
  ])

  project            = var.gcp_project_id
  service            = each.value
  disable_on_destroy = false
}

# Single versioned state bucket; each environment is isolated by a `prefix` in its backend (FR-020).
resource "google_storage_bucket" "tfstate" {
  name     = var.bucket_name
  project  = var.gcp_project_id
  location = var.gcp_region

  # Keep prior state versions so a bad apply can be recovered.
  versioning {
    enabled = true
  }

  # IAM-only access (no legacy ACLs) — required for least-privilege state access.
  uniform_bucket_level_access = true

  # Guard against accidental `tofu destroy` wiping all environments' state.
  force_destroy = false

  depends_on = [google_project_service.required]
}

output "bucket_name" {
  description = "Use this as `bucket` in each environment's backend.tf."
  value       = google_storage_bucket.tfstate.name
}

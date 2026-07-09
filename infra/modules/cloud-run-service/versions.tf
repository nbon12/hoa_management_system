terraform {
  required_version = ">= 1.8.0"

  required_providers {
    # Same pin as the consuming modules (010 FR-021): ~> 5.0 keeps the Cloud Run v2 shapes used here.
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

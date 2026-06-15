variable "gcp_project_id" {
  description = "GCP project that owns the state bucket and into which environments are provisioned."
  type        = string
}

variable "gcp_region" {
  description = "Location for the versioned state bucket (also the default environment region)."
  type        = string
  default     = "us-central1"
}

variable "bucket_name" {
  description = "Globally-unique name for the versioned GCS bucket that holds remote state for every environment (prefix-isolated). Record this value for each environment's backend.tf."
  type        = string
}

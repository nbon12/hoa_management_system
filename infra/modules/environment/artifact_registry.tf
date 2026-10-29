# Container registry for the backend image. CI pushes here as the deployer SA (WIF, no registry key)
# and Cloud Run pulls from here. Cloud Run used to pull the Docker Hub image through mirror.gcr.io,
# which answers 429 for every tag it has not cached yet, so each freshly pushed tag failed to deploy
# (2026-10-27). Cloud Run's service agent reads a same-project repository without an extra grant.
resource "google_artifact_registry_repository" "images" {
  project       = var.gcp_project_id
  location      = var.gcp_region
  repository_id = "images-${var.env_name}"
  format        = "DOCKER"
  description   = "nekohoa ${var.env_name} container images (pushed by CI via WIF, pulled by Cloud Run)"

  # Per-PR images (pr-<n>-<sha>) are dead once the PR pushes again or the sweep reclaims its idle
  # environment (7 days), so delete them after 14. Main images (<sha>, latest) are kept: Dev runs one
  # of them and a rollback needs the others.
  cleanup_policies {
    id     = "delete-pr-images"
    action = "DELETE"
    condition {
      tag_state    = "TAGGED"
      tag_prefixes = ["pr-"]
      older_than   = "1209600s" # 14 days
    }
  }
}

# The deployer SA pushes images. It is project Owner today (iam.tf), so this grant records the need
# rather than adding access, like deployer_run_admin.
resource "google_artifact_registry_repository_iam_member" "deployer_writer" {
  project    = google_artifact_registry_repository.images.project
  location   = google_artifact_registry_repository.images.location
  repository = google_artifact_registry_repository.images.name
  role       = "roles/artifactregistry.writer"
  member     = "serviceAccount:${google_service_account.deployer.email}"
}

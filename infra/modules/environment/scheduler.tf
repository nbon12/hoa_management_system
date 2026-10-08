# 027-board-arc-review (research R6): hourly Cloud Scheduler job that calls the architectural review
# sweep — pre-deadline board reminders, per-community lapse rules and outbox dispatch. Cloud Run scales
# to zero, so an in-process timer would never fire; the endpoint is idempotent, so an extra or retried
# run is harmless. Authenticated by the shared X-Scheduler-Secret header (same secret the API reads as
# Jobs__SchedulerSharedSecret). The header takes the tfvars value of scheduler-secret: rotate it through
# tfvars (not out-of-band) so the job and the API stay in step. PR environments get no job; run the
# sweep there by hand (see specs/027-board-arc-review/quickstart.md).

resource "google_cloud_scheduler_job" "arc_sweep" {
  project          = var.gcp_project_id
  region           = var.gcp_region
  name             = "nekohoa-arc-sweep-${var.env_name}"
  description      = "Hourly architectural review sweep (reminders, lapse rules, email dispatch)."
  schedule         = "7 * * * *"
  time_zone        = "Etc/UTC"
  attempt_deadline = "180s"

  retry_config {
    retry_count = 1
  }

  http_target {
    http_method = "POST"
    uri         = "${google_cloud_run_v2_service.api.uri}/api/v1/architectural/jobs/sweep"

    headers = {
      "X-Scheduler-Secret" = google_secret_manager_secret_version.operator["scheduler-secret"].secret_data
    }
  }
}

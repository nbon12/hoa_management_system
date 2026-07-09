# Per-PR Cloud Run API service (US2, research D1/D4) — provisioned through the SHARED core module
# (015 US6, FR-019). This wrapper contributes only the per-PR variant: nekohoa-api-pr-<n>,
# TTL/sweep labels, the SHARED runtime SA, ASPNETCORE_ENVIRONMENT=Dev (reuses all Dev behavior +
# the *.pages.dev CORS allowance), and the 7-shared + 2-per-PR secret wiring. Image is
# PIPELINE-OWNED (the workflow deploys :pr-<n>-<sha> image-only AFTER the Stripe webhook secret is
# written); the shared module's ignore_changes keeps tofu from reverting it.

locals {
  # .NET env key → secret ref. Seven SHARED dev-* operator secrets (referenced by name as
  # ${shared_secret_prefix}-<short>, reused from Dev, never recreated) + two PER-PR secrets this
  # module creates (db-connection, stripe-webhook).
  secret_env_refs = merge(
    {
      for key, short in {
        "Jwt__Secret"                 = "jwt-secret"
        "Sentry__Dsn"                 = "sentry-dsn"
        "Stripe__SecretKey"           = "stripe-secret-key"
        "Storage__ServiceUrl"         = "storage-service-url"
        "Storage__AccessKey"          = "storage-access-key"
        "Storage__SecretKey"          = "storage-secret-key"
        "Jobs__SchedulerSharedSecret" = "scheduler-secret"
      } : key => "${var.shared_secret_prefix}-${short}"
    },
    {
      "ConnectionStrings__DefaultConnection" = google_secret_manager_secret.db_connection.secret_id
      "Stripe__WebhookSigningSecret"         = google_secret_manager_secret.stripe_webhook.secret_id
    },
  )
}

module "api_service" {
  source = "../cloud-run-service"

  gcp_project_id          = var.gcp_project_id
  gcp_region              = var.gcp_region
  service_name            = "nekohoa-api-pr-${var.pr_number}"
  labels                  = var.labels
  runtime_service_account = var.runtime_service_account
  container_image         = var.container_image
  aspnet_environment      = "Dev" # config-validation known set; inherits migrations/seed/Swagger/CORS-preview (research D4)
  stripe_publishable_key  = var.stripe_publishable_key
  documents_bucket_name   = cloudflare_r2_bucket.documents.name
  secret_env_refs         = local.secret_env_refs

  depends_on = [
    google_secret_manager_secret_version.db_connection,
    google_secret_manager_secret_version.stripe_webhook_placeholder,
    google_secret_manager_secret_iam_member.runtime_pr_accessor,
  ]
}

# State migration (015 US6): live PR envs must survive the refactor without destroy/recreate.
moved {
  from = google_cloud_run_v2_service.api
  to   = module.api_service.google_cloud_run_v2_service.api
}

moved {
  from = google_cloud_run_v2_service_iam_member.public
  to   = module.api_service.google_cloud_run_v2_service_iam_member.public
}

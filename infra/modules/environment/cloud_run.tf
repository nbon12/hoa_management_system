# Cloud Run API service (matrix rows 1-8) — provisioned through the SHARED core module
# (015 US6, FR-019: one definition instead of the former ~150-line near-twin of
# modules/pr-environment). This wrapper contributes only the long-lived-environment variant:
# name nekohoa-api-${env_name}, the module-created runtime SA, this module's nine secrets, and
# the optional custom-domain mapping.

locals {
  # .NET env key → this module's Secret Manager secret id (the nine refs, matrix rows 8-16).
  # The literal-name fidelity lives in secrets.tf; here we just wire env vars to secret ids.
  secret_env_refs = {
    "ConnectionStrings__DefaultConnection" = google_secret_manager_secret.this["db-connection"].secret_id
    "Jwt__Secret"                          = google_secret_manager_secret.this["jwt-secret"].secret_id
    "Sentry__Dsn"                          = google_secret_manager_secret.this["sentry-dsn"].secret_id
    "Stripe__SecretKey"                    = google_secret_manager_secret.this["stripe-secret-key"].secret_id
    "Stripe__WebhookSigningSecret"         = google_secret_manager_secret.this["stripe-webhook-secret"].secret_id
    "Storage__ServiceUrl"                  = google_secret_manager_secret.this["storage-service-url"].secret_id
    "Storage__AccessKey"                   = google_secret_manager_secret.this["storage-access-key"].secret_id
    "Storage__SecretKey"                   = google_secret_manager_secret.this["storage-secret-key"].secret_id
    "Jobs__SchedulerSharedSecret"          = google_secret_manager_secret.this["scheduler-secret"].secret_id
  }
}

module "api_service" {
  source = "../cloud-run-service"

  gcp_project_id          = var.gcp_project_id
  gcp_region              = var.gcp_region
  service_name            = "nekohoa-api-${var.env_name}"
  runtime_service_account = google_service_account.runtime.email
  container_image         = var.container_image
  aspnet_environment      = var.aspnet_environment
  stripe_publishable_key  = var.stripe_publishable_key
  documents_bucket_name   = cloudflare_r2_bucket.documents.name
  secret_env_refs         = local.secret_env_refs

  # Secrets + accessor IAM must exist before the service references them.
  depends_on = [
    google_secret_manager_secret_version.db_connection,
    google_secret_manager_secret_version.operator,
    google_secret_manager_secret_iam_member.runtime_accessor,
  ]
}

# State migration (015 US6): the service moved INTO the shared module — never destroy/recreate a
# live environment over a refactor.
moved {
  from = google_cloud_run_v2_service.api
  to   = module.api_service.google_cloud_run_v2_service.api
}

moved {
  from = google_cloud_run_v2_service_iam_member.public
  to   = module.api_service.google_cloud_run_v2_service_iam_member.public
}

# Custom domain mapping for the API (matrix row 20). The Cloudflare record for this host is in
# cloudflare.tf; grey-cloud first lets Google issue the managed cert (FR-018/019).
resource "google_cloud_run_domain_mapping" "api" {
  # Requires one-time Google domain-ownership verification; gated so the environment applies before
  # that manual step. Enable via var.enable_api_domain once the domain is verified (FR-018/019).
  count = var.enable_api_domain ? 1 : 0

  project  = var.gcp_project_id
  location = var.gcp_region
  name     = var.api_domain

  metadata {
    namespace = var.gcp_project_id
  }

  spec {
    route_name = module.api_service.name
  }
}

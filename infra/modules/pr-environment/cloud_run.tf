# Per-PR Cloud Run v2 API service (US2, research D1/D4). nekohoa-api-pr-<n>, scale-to-zero, public
# invoker, ASPNETCORE_ENVIRONMENT=Dev (reuses all Dev behavior + the *.pages.dev CORS allowance), runs
# as the SHARED runtime SA. Wiring: 7 shared dev-* operator secrets + 2 per-PR secrets (db-connection,
# stripe-webhook) + the per-PR R2 bucket. Image is PIPELINE-OWNED (workflow deploys :pr-<n>-<sha>
# image-only AFTER the Stripe webhook secret is written); ignore_changes keeps tofu from reverting it.

locals {
  # .NET env key → SHARED operator secret short-name (referenced as ${shared_secret_prefix}-<name>,
  # e.g. dev-jwt-secret). Reused from Dev, never recreated. Mirrors modules/environment secret_env minus
  # the two that are per-PR (db-connection, stripe-webhook-secret).
  shared_secret_env = {
    "Jwt__Secret"                 = "jwt-secret"
    "Sentry__Dsn"                 = "sentry-dsn"
    "Stripe__SecretKey"           = "stripe-secret-key"
    "Storage__ServiceUrl"         = "storage-service-url"
    "Storage__AccessKey"          = "storage-access-key"
    "Storage__SecretKey"          = "storage-secret-key"
    "Jobs__SchedulerSharedSecret" = "scheduler-secret"
  }

  # .NET env key → PER-PR Secret Manager secret id (this module's own secrets).
  per_pr_secret_env = {
    "ConnectionStrings__DefaultConnection" = google_secret_manager_secret.db_connection.secret_id
    "Stripe__WebhookSigningSecret"         = google_secret_manager_secret.stripe_webhook.secret_id
  }
}

resource "google_cloud_run_v2_service" "api" {
  project  = var.gcp_project_id
  name     = "nekohoa-api-pr-${var.pr_number}"
  location = var.gcp_region
  ingress  = "INGRESS_TRAFFIC_ALL"
  labels   = var.labels

  template {
    service_account = var.runtime_service_account

    scaling {
      min_instance_count = 0 # scale-to-zero — idle PR envs cost ~$0 (SC-008)
    }

    containers {
      image = var.container_image

      ports {
        container_port = 8080
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Dev" # config-validation known set; inherits migrations/seed/Swagger/CORS-preview (research D4)
      }

      env {
        name  = "Stripe__PublishableKey"
        value = var.stripe_publishable_key
      }

      # Per-PR R2 document bucket; PATH-STYLE required by R2 (same as Dev).
      env {
        name  = "Storage__BucketName"
        value = cloudflare_r2_bucket.documents.name
      }
      env {
        name  = "Storage__ForcePathStyle"
        value = "true"
      }

      # Shared operator secrets (dev-*), latest version.
      dynamic "env" {
        for_each = local.shared_secret_env
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = "${var.shared_secret_prefix}-${env.value}"
              version = "latest"
            }
          }
        }
      }

      # Per-PR secrets (db-connection, stripe-webhook), latest version.
      dynamic "env" {
        for_each = local.per_pr_secret_env
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = env.value
              version = "latest"
            }
          }
        }
      }

      startup_probe {
        # First boot runs EF migrations (the PR's new ones) before /health; seed is a no-op (forked
        # branch already carries it). Allow ~5 min (30 × 10s).
        initial_delay_seconds = 10
        timeout_seconds       = 5
        period_seconds        = 10
        failure_threshold     = 30
        http_get {
          path = "/health"
        }
      }

      liveness_probe {
        timeout_seconds   = 5
        period_seconds    = 30
        failure_threshold = 3
        http_get {
          path = "/health"
        }
      }
    }
  }

  # Image + revision + traffic are pipeline-owned (the workflow image-only deploy); scaling churns on
  # gcloud deploys. Mirror modules/environment so an infra apply never fights the deploy (FR-016).
  lifecycle {
    ignore_changes = [
      template[0].containers[0].image,
      template[0].revision,
      template[0].scaling,
      client,
      client_version,
      traffic,
    ]
  }

  depends_on = [
    google_secret_manager_secret_version.db_connection,
    google_secret_manager_secret_version.stripe_webhook_placeholder,
    google_secret_manager_secret_iam_member.runtime_pr_accessor,
  ]
}

# Public, unauthenticated access so the smoke suites can reach the per-PR API (same as Dev).
resource "google_cloud_run_v2_service_iam_member" "public" {
  project  = var.gcp_project_id
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

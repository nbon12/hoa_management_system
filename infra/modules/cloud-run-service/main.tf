# Cloud Run v2 API service — shared core (015 US6, FR-019). Scale-to-zero, port 8080, /health
# startup+liveness probes, secret-backed env refs, public invoker. The image is PIPELINE-OWNED —
# ignore_changes keeps an infra apply from reverting the revision the deploy job pushes
# (009 FR-005/006/007/008); tofu stays the single declarative owner of everything else.

resource "google_cloud_run_v2_service" "api" {
  project  = var.gcp_project_id
  name     = var.service_name
  location = var.gcp_region
  ingress  = "INGRESS_TRAFFIC_ALL"
  labels   = var.labels

  template {
    service_account = var.runtime_service_account

    scaling {
      min_instance_count = 0 # scale-to-zero — idle envs cost ~$0
    }

    containers {
      image = var.container_image

      ports {
        container_port = 8080
      }

      # Literal runtime environment selector (config-validation known set).
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.aspnet_environment
      }

      # Stripe publishable key — non-secret (browser-safe), so a plain env var rather than a
      # Secret Manager ref. Required by StripeOptionsValidator at startup.
      env {
        name  = "Stripe__PublishableKey"
        value = var.stripe_publishable_key
      }

      # Document storage (Cloudflare R2). Non-secret config: the bucket the caller provisions, and
      # R2 requires PATH-STYLE addressing (the app default false yields virtual-host URLs R2
      # rejects). The R2 access key / secret / endpoint come from Secret Manager (storage-*).
      env {
        name  = "Storage__BucketName"
        value = var.documents_bucket_name
      }
      env {
        name  = "Storage__ForcePathStyle"
        value = "true"
      }

      # Secret-backed env vars, each pointing at its secret's latest version.
      dynamic "env" {
        for_each = var.secret_env_refs
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
        # First boot runs EF migrations (+ seed where enabled) before /health is served; allow up
        # to ~5 min (30 × 10s). Subsequent cold starts pass quickly.
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

  # The deploy pipeline owns the image AND traffic; never let an infra apply revert them.
  # image is the live :sha the pipeline pushes; client/client_version churn on every gcloud
  # deploy; traffic is the canary split (candidate tag → 100%) the deploy job manages;
  # template[0].revision is stamped per gcloud deploy; the pipeline's image-only deploys re-emit
  # scaling without the explicit min=0 (the API default), which tofu would perpetually re-add.
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
}

# Public access: allow-unauthenticated (Cloudflare fronts public traffic at the edge).
resource "google_cloud_run_v2_service_iam_member" "public" {
  project  = var.gcp_project_id
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

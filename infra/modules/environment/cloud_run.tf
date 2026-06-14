# Cloud Run v2 API service (matrix rows 1-8). Name nekohoa-api-${env_name} (Dev → nekohoa-api-dev),
# scale-to-zero, port 8080, ASPNETCORE_ENVIRONMENT from var.aspnet_environment ("Dev"), /health
# probes, the nine secret env refs, runtime SA. The image is PIPELINE-OWNED — ignore_changes keeps an
# infra apply from reverting the revision the 009 deploy job pushes (FR-005/006/007/008).

locals {
  # .NET env key → unprefixed secret name (the nine refs, matrix rows 8-16). The literal-name fidelity
  # lives in secrets.tf; here we just wire each container env var to its secret's latest version.
  secret_env = {
    "ConnectionStrings__DefaultConnection" = "db-connection"
    "Jwt__Secret"                          = "jwt-secret"
    "Sentry__Dsn"                          = "sentry-dsn"
    "Stripe__SecretKey"                    = "stripe-secret-key"
    "Stripe__WebhookSigningSecret"         = "stripe-webhook-secret"
    "Storage__ServiceUrl"                  = "storage-service-url"
    "Storage__AccessKey"                   = "storage-access-key"
    "Storage__SecretKey"                   = "storage-secret-key"
    "Jobs__SchedulerSharedSecret"          = "scheduler-secret"
  }
}

resource "google_cloud_run_v2_service" "api" {
  project  = var.gcp_project_id
  name     = "nekohoa-api-${var.env_name}"
  location = var.gcp_region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.runtime.email

    scaling {
      min_instance_count = 0
    }

    containers {
      image = var.container_image

      ports {
        container_port = 8080
      }

      # Literal runtime environment selector (matrix row 4). Dev → "Dev".
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.aspnet_environment
      }

      # Stripe publishable key — non-secret (browser-safe), so a plain env var rather than a Secret
      # Manager ref. Required by StripeOptionsValidator at startup; deployed envs supply it here.
      env {
        name  = "Stripe__PublishableKey"
        value = var.stripe_publishable_key
      }

      # Document storage (Cloudflare R2). Non-secret config: the bucket is the one this module
      # provisions (app default "hoa-documents" is the local-MinIO name and would 404 on R2), and R2
      # requires PATH-STYLE addressing (the app default false yields virtual-host URLs R2 rejects).
      # The R2 access key / secret / endpoint come from Secret Manager (storage-* secrets).
      env {
        name  = "Storage__BucketName"
        value = cloudflare_r2_bucket.documents.name
      }
      env {
        name  = "Storage__ForcePathStyle"
        value = "true"
      }

      # The nine secret-backed env vars, each pointing at its Secret Manager secret's latest version.
      dynamic "env" {
        for_each = local.secret_env
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.this[env.value].secret_id
              version = "latest"
            }
          }
        }
      }

      startup_probe {
        # First boot runs EF migrations + full data seed before /health is served; allow up to ~5 min
        # (30 × 10s). Subsequent cold starts skip the seed and pass quickly.
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

  # The deploy pipeline owns the image AND traffic; never let an infra apply revert them (FR-007).
  # tofu is the single declarative owner of all OTHER config (env vars, secrets, probes, scaling, SA),
  # so the pipeline deploys image-only and shifts traffic (canary tags) without drift here. image is
  # the live :sha pushed by 009; client/client_version churn on every gcloud deploy; traffic is the
  # canary split (candidate tag → 100%) the deploy job manages.
  lifecycle {
    ignore_changes = [
      template[0].containers[0].image,
      client,
      client_version,
      traffic,
    ]
  }

  # Secrets + accessor IAM must exist before the service references them.
  depends_on = [
    google_secret_manager_secret_version.db_connection,
    google_secret_manager_secret_version.operator,
    google_secret_manager_secret_iam_member.runtime_accessor,
  ]
}

# Public access: allow-unauthenticated (matrix row 5).
resource "google_cloud_run_v2_service_iam_member" "public" {
  project  = var.gcp_project_id
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
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
    route_name = google_cloud_run_v2_service.api.name
  }
}

# Per-PR Secret Manager entries (research D1, FR-010). ONLY the secrets that genuinely differ per PR
# live here: `pr-<n>-db-connection` (this PR's Neon branch). The 7 shared operator secrets
# (jwt, sentry, stripe-secret-key, storage-*, scheduler) are reused from Dev by id and never recreated;
# `pr-<n>-stripe-webhook` is added in US2 (cloud_run wiring) since it needs the running API URL.
#
# Labels carry the cost-attribution / sweep tags so the SC-008 budget filter and the reclaim sweep can
# find per-PR GCP spend.

resource "google_secret_manager_secret" "db_connection" {
  project   = var.gcp_project_id
  secret_id = "pr-${var.pr_number}-db-connection"
  labels    = var.labels

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "db_connection" {
  secret      = google_secret_manager_secret.db_connection.id
  secret_data = local.db_connection_string
}

# Per-PR Stripe webhook signing secret (US2, research D9). The real `whsec_…` value is written by
# scripts/stripe-webhook-register.sh AFTER the API URL exists; we create a PLACEHOLDER first version so
# Cloud Run can mount `latest` at create time, and ignore_changes so the script's real version (which
# becomes the new latest) is not reverted by a later apply.
resource "google_secret_manager_secret" "stripe_webhook" {
  project   = var.gcp_project_id
  secret_id = "pr-${var.pr_number}-stripe-webhook"
  labels    = var.labels

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "stripe_webhook_placeholder" {
  secret      = google_secret_manager_secret.stripe_webhook.id
  secret_data = "whsec_placeholder_pending_registration"

  lifecycle {
    ignore_changes = [secret_data]
  }
}

# The SHARED runtime SA must be able to read this PR's per-PR secrets (least privilege — the SA is
# already an accessor on the dev-* shared secrets it also consumes).
resource "google_secret_manager_secret_iam_member" "runtime_pr_accessor" {
  for_each = {
    db-connection  = google_secret_manager_secret.db_connection.secret_id
    stripe-webhook = google_secret_manager_secret.stripe_webhook.secret_id
  }

  project   = var.gcp_project_id
  secret_id = each.value
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.runtime_service_account}"
}

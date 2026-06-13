# The nine Secret Manager entries the 009 deploy job maps to .NET env keys (matrix rows 8-16).
# Secret IDs are LITERAL per the contract: secret_id = "${var.secret_prefix}-<name>" → with
# secret_prefix="dev" these resolve to the exact dev-* IDs (FR-013/029). dev-db-connection is
# auto-wired from Neon (FR-014); the eight operator secrets take their first version from tfvars and
# then ignore body changes so out-of-band rotation doesn't show as drift (FR-015).

locals {
  # The eight operator-supplied secret names (unprefixed). db-connection is the 9th, auto-wired below.
  operator_secret_names = [
    "jwt-secret",
    "sentry-dsn",
    "stripe-secret-key",
    "stripe-webhook-secret",
    "storage-service-url",
    "storage-access-key",
    "storage-secret-key",
    "scheduler-secret",
  ]

  all_secret_names = concat(["db-connection"], local.operator_secret_names)
}

resource "google_secret_manager_secret" "this" {
  for_each = toset(local.all_secret_names)

  project   = var.gcp_project_id
  secret_id = "${var.secret_prefix}-${each.value}"

  replication {
    auto {}
  }
}

# dev-db-connection: payload is the Neon pooled .NET keyword string (local from neon.tf).
resource "google_secret_manager_secret_version" "db_connection" {
  secret      = google_secret_manager_secret.this["db-connection"].id
  secret_data = local.db_connection_string
}

# The eight operator secrets: first version from tfvars, body ignored thereafter (operator rotation).
# for_each is keyed off the NON-sensitive name list (a sensitive value cannot drive for_each); the
# sensitive payload is looked up by key in the body, which is allowed.
resource "google_secret_manager_secret_version" "operator" {
  for_each = toset(local.operator_secret_names)

  secret      = google_secret_manager_secret.this[each.key].id
  secret_data = var.operator_secrets[each.key]

  lifecycle {
    ignore_changes = [secret_data]
  }
}

# Runtime SA may access each secret (least privilege — per-secret, not project-wide) (FR-009).
resource "google_secret_manager_secret_iam_member" "runtime_accessor" {
  for_each = google_secret_manager_secret.this

  project   = var.gcp_project_id
  secret_id = each.value.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.runtime.email}"
}

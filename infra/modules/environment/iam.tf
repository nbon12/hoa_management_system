# Service accounts + Workload Identity Federation (FR-009/010/011/012). Least privilege (§7): the
# runtime SA only reads its secrets; the deployer SA can deploy Cloud Run revisions and act as the
# runtime SA; GitHub authenticates via WIF — no long-lived keys (FR-027).

# --- Runtime SA: the identity the Cloud Run service runs as (secret accessor only). ---
resource "google_service_account" "runtime" {
  project      = var.gcp_project_id
  account_id   = "nekohoa-run-${var.env_name}"
  display_name = "nekohoa ${var.env_name} Cloud Run runtime"
}

# --- Deployer SA: impersonated by GitHub Actions to push new revisions. ---
resource "google_service_account" "deployer" {
  project      = var.gcp_project_id
  account_id   = "nekohoa-deploy-${var.env_name}"
  display_name = "nekohoa ${var.env_name} deployer (GitHub Actions via WIF)"
}

# Deployer may manage Cloud Run services in the project (FR-010).
resource "google_project_iam_member" "deployer_run_admin" {
  project = var.gcp_project_id
  role    = "roles/run.admin"
  member  = "serviceAccount:${google_service_account.deployer.email}"
}

# Deployer may "actAs" the runtime SA so a deploy can set the service's identity (FR-010).
resource "google_service_account_iam_member" "deployer_sa_user" {
  service_account_id = google_service_account.runtime.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.deployer.email}"
}

# --- Workload Identity Federation: lets this GitHub repo impersonate the deployer SA via OIDC. ---
# Pool id derived from env_name so multiple environments can coexist in one project (reuse-safe).
resource "google_iam_workload_identity_pool" "github" {
  project                   = var.gcp_project_id
  workload_identity_pool_id = "github-pool-${var.env_name}"
  display_name              = "GitHub Actions (${var.env_name})"
  description               = "OIDC federation for ${var.github_repository} → deployer SA"
}

resource "google_iam_workload_identity_pool_provider" "github" {
  project                            = var.gcp_project_id
  workload_identity_pool_id          = google_iam_workload_identity_pool.github.workload_identity_pool_id
  workload_identity_pool_provider_id = "github-provider"
  display_name                       = "GitHub OIDC"

  oidc {
    issuer_uri = "https://token.actions.githubusercontent.com"
  }

  attribute_mapping = {
    "google.subject"       = "assertion.sub"
    "attribute.repository" = "assertion.repository"
  }

  # Repo-scoped (NOT ref-scoped) so both PR plans and merge applies authenticate (FR-012, research §5).
  # Pinned to the full owner/repo — a broader assertion.repository_owner would let any org repo in.
  attribute_condition = "assertion.repository == \"${var.github_repository}\""
}

# Bind workloadIdentityUser for the specific repo's federated principals onto the deployer SA.
resource "google_service_account_iam_member" "wif_deployer" {
  service_account_id = google_service_account.deployer.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/${google_iam_workload_identity_pool.github.name}/attribute.repository/${var.github_repository}"
}

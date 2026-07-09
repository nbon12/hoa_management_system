# Published outputs (013 contracts/pr-environment-module.md). Consumed by the pr-env.yml workflow to
# wire the frontend build, the Stripe webhook target, and the smoke tests. The US2 outputs (api_url,
# web_branch, stripe_webhook_secret_id) are added with cloud_run.tf.

output "neon_branch_id" {
  description = "Per-PR Neon branch id (diagnostics / sweep)."
  value       = neon_branch.pr.id
}

output "db_connection_secret_id" {
  description = "Secret Manager id of the per-PR db-connection secret (Cloud Run wiring)."
  value       = google_secret_manager_secret.db_connection.secret_id
}

output "db_connection_string" {
  description = "Per-PR Neon pooled .NET connection string (consumed by the US1 storage/db test job)."
  value       = local.db_connection_string
  sensitive   = true
}

output "r2_bucket_name" {
  description = "Per-PR R2 document bucket name (Cloud Run Storage__BucketName + test wiring)."
  value       = cloudflare_r2_bucket.documents.name
}

output "web_branch" {
  description = "Cloudflare Pages branch alias for this PR's frontend deploy → pr-<n>.nekohoa-dev.pages.dev."
  value       = "pr-${var.pr_number}"
}

output "api_url" {
  description = "Per-PR Cloud Run service URL (frontend API base, Stripe webhook target, smoke tests)."
  value       = module.api_service.uri
}

output "cloud_run_service_name" {
  description = "Per-PR Cloud Run service name (for the workflow's image-only gcloud deploy)."
  value       = module.api_service.name
}

output "stripe_webhook_secret_id" {
  description = "Secret Manager id the Stripe webhook register script writes the signing secret into."
  value       = google_secret_manager_secret.stripe_webhook.secret_id
}

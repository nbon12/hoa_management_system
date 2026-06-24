# Re-export the module outputs at the root so the pr-env.yml workflow can read them with
# `tofu output -raw <name>` (the module's outputs are not visible at the root otherwise).

output "api_url" {
  value = module.pr_environment.api_url
}

output "cloud_run_service_name" {
  value = module.pr_environment.cloud_run_service_name
}

output "r2_bucket_name" {
  value = module.pr_environment.r2_bucket_name
}

output "web_branch" {
  value = module.pr_environment.web_branch
}

output "neon_branch_id" {
  value = module.pr_environment.neon_branch_id
}

output "stripe_webhook_secret_id" {
  value = module.pr_environment.stripe_webhook_secret_id
}

output "db_connection_secret_id" {
  value = module.pr_environment.db_connection_secret_id
}

output "db_connection_string" {
  value     = module.pr_environment.db_connection_string
  sensitive = true
}

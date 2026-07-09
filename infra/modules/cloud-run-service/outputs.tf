output "uri" {
  description = "The service's run.app URI."
  value       = google_cloud_run_v2_service.api.uri
}

output "name" {
  description = "The service name."
  value       = google_cloud_run_v2_service.api.name
}

output "location" {
  description = "The service region."
  value       = google_cloud_run_v2_service.api.location
}

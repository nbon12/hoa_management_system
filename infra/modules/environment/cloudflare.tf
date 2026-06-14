# Cloudflare frontend + edge (matrix rows 18-20). Pages project nekohoa-${env_name}, the R2 documents
# bucket, and the two DNS records. The API record's orange/grey cloud is var.api_dns_proxied so the
# cert flip is a one-line, reviewable change (FR-016/017/018/019).

resource "cloudflare_pages_project" "frontend" {
  account_id        = var.cloudflare_account_id
  name              = "nekohoa-${var.env_name}"
  production_branch = "main"
}

# Attach the custom frontend domain (Dev: dev.nekohoa.com) to the Pages project.
resource "cloudflare_pages_domain" "frontend" {
  account_id   = var.cloudflare_account_id
  project_name = cloudflare_pages_project.frontend.name
  domain       = var.frontend_domain
}

# R2 bucket for Dev documents (the hosted substitute for local MinIO).
resource "cloudflare_r2_bucket" "documents" {
  account_id = var.cloudflare_account_id
  name       = "nekohoa-${var.env_name}-documents"
}

# Frontend DNS: dev.nekohoa.com → the Pages project, proxied.
resource "cloudflare_record" "frontend" {
  zone_id = var.cloudflare_zone_id
  name    = var.frontend_domain
  type    = "CNAME"
  content = cloudflare_pages_project.frontend.subdomain
  proxied = true
}

# API DNS: api-dev.nekohoa.com → Cloud Run's Google-hosted target. proxied toggles the cert flow:
# false (grey-cloud) for first apply so Google issues the cert, then true for proxied Full(strict).
resource "cloudflare_record" "api" {
  # Created alongside the custom-domain mapping (gated on Google domain verification).
  count = var.enable_api_domain ? 1 : 0

  zone_id = var.cloudflare_zone_id
  name    = var.api_domain
  type    = "CNAME"
  content = "ghs.googlehosted.com"
  proxied = var.api_dns_proxied

  depends_on = [google_cloud_run_domain_mapping.api]
}

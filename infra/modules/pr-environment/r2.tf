# Per-PR Cloudflare R2 document bucket (research D1, FR-002). Isolated from Dev's bucket and every
# other PR's. R2 has no GCP labels (the SC-008 budget tracks GCP cost only); isolation is by name.
resource "cloudflare_r2_bucket" "documents" {
  account_id = var.cloudflare_account_id
  name       = "nekohoa-pr-${var.pr_number}-documents"
}

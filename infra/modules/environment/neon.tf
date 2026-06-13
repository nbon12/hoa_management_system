# Neon (community provider kislerdm/neon — see versions.tf). One project per environment gives the Dev
# database full isolation from any Staging/Prod (FR-004, §10). We assemble the connection string as a
# .NET KEYWORD string from the POOLED endpoint host — never the provider's postgresql:// URI (FR-003).
#
# ⚠️ Provider-attribute names below (database_host / role password / endpoint host) are confirmed at
# the operator's `tofu init && tofu validate` gate (T020) — the registry pin is exact (= 0.6.3) but
# the schema is verified there per research.md §2 (deferred-to-implementation item).

resource "neon_project" "this" {
  name      = "nekohoa-${var.env_name}"
  region_id = var.neon_region_id

  # Point-in-time restore window. The Neon free/launch plan caps this at 6h (21600s); the provider
  # default (604800 = 7d) is rejected on those plans. 6h is plenty for a Dev environment.
  history_retention_seconds = 21600

  # org_id is assigned by the API from the key's account and is immutable; it is not set in config,
  # so without this the provider reads it back and plans `org_id -> null`, which FORCES REPLACEMENT on
  # every apply (infinite project churn). Ignore it so the project is stable.
  lifecycle {
    ignore_changes = [org_id]
  }
}

# The environment's working branch. Named from env_name (Dev → "dev", matching data-model) rather
# than a literal so Staging/Prod reuse the module unchanged (T031 audit; SC-009).
resource "neon_branch" "main" {
  project_id = neon_project.this.id
  name       = var.env_name
}

# Compute endpoint for the branch, with the connection pooler enabled (pooled host = "-pooler" form).
resource "neon_endpoint" "main" {
  project_id     = neon_project.this.id
  branch_id      = neon_branch.main.id
  type           = "read_write"
  pooler_enabled = true
  pooler_mode    = "transaction"
}

resource "neon_role" "app" {
  project_id = neon_project.this.id
  branch_id  = neon_branch.main.id
  name       = "nekohoa_app"

  # Role/database operations go through the branch's read-write endpoint, which must exist first.
  depends_on = [neon_endpoint.main]
}

resource "neon_database" "app" {
  project_id = neon_project.this.id
  branch_id  = neon_branch.main.id
  name       = "nekohoa"
  owner_name = neon_role.app.name

  depends_on = [neon_endpoint.main]
}

locals {
  # The pooled host is the endpoint host with "-pooler" inserted into the endpoint-id segment, e.g.
  # ep-cool-darkness-123.us-east-2.aws.neon.tech → ep-cool-darkness-123-pooler.us-east-2.aws.neon.tech
  neon_pooled_host = replace(neon_endpoint.main.host, "/^(ep-[a-z0-9-]+?)\\./", "$1-pooler.")

  # .NET keyword string consumed as ConnectionStrings__DefaultConnection (matrix rows 8 & 17).
  # Matches ^Host=.*;SSL Mode=Require;Channel Binding=Require$ and contains no postgresql:// (FR-003).
  db_connection_string = join(";", [
    "Host=${local.neon_pooled_host}",
    "Database=${neon_database.app.name}",
    "Username=${neon_role.app.name}",
    "Password=${neon_role.app.password}",
    "SSL Mode=Require",
    "Channel Binding=Require",
  ])
}

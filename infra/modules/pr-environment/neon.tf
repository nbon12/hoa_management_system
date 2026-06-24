# <!-- REPOWISE:START domain=pr-environment -->
# Per-PR Neon branch (research D1/D3). A copy-on-write FORK of the pre-seeded `pr-base` branch inside
# the SHARED Neon project — the project is never recreated. The fork already carries the deterministic
# seed and the inherited `nekohoa_app` role + `nekohoa` database (identical password to pr-base), so we
# create ONLY a new compute endpoint and assemble the .NET connection string from its pooled host.
#
# ⚠️ kislerdm/neon 0.6.3 attribute names confirmed via `tofu providers schema` (parent_id fork,
# pooler_enabled endpoint). Verified again at the `tofu validate` gate (T006/T016).

resource "neon_branch" "pr" {
  project_id = var.neon_project_id
  parent_id  = var.neon_base_branch_id
  name       = "pr-${var.pr_number}"
}

# New read-write compute endpoint for the per-PR branch. Transaction-mode connection pooling multiplexes
# the app's connections so each PR env holds few real Postgres connections — keeping total connections
# bounded across many concurrent PR envs (constitution §8, T040). The endpoint autosuspends on the
# account's default interval when idle (scale-to-zero economics, SC-008). NOTE: autoscaling CU limits and
# a custom suspend_timeout_seconds are paid-tier Neon features (HTTP 412 on this account), so they are
# intentionally omitted — matches the working Dev module's endpoint.
resource "neon_endpoint" "pr" {
  project_id     = var.neon_project_id
  branch_id      = neon_branch.pr.id
  type           = "read_write"
  pooler_enabled = true
  pooler_mode    = "transaction"
}

locals {
  # Pooled host: insert "-pooler" into the endpoint-id segment (same transform as modules/environment).
  neon_pooled_host = replace(neon_endpoint.pr.host, "/^(ep-[a-z0-9-]+?)\\./", "$1-pooler.")

  # .NET keyword string consumed as ConnectionStrings__DefaultConnection. Role/database/password are
  # inherited from pr-base (FR-003); host is this PR's own endpoint (FR-002 isolation).
  db_connection_string = join(";", [
    "Host=${local.neon_pooled_host}",
    "Database=${var.neon_database_name}",
    "Username=${var.neon_role_name}",
    "Password=${var.neon_role_password}",
    "SSL Mode=Require",
    "Channel Binding=Require",
  ])
}
# <!-- REPOWISE:END -->

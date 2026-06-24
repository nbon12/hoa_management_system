# SC-008 cost guardrail (013, research D10). A SINGLE account-level budget — created once, not per PR —
# filtered to GCP spend carrying the `pr-env` label, so it captures the per-PR Cloud Run cost (the only
# meaningful variable GCP cost; CI minutes are free on the public repo, R2/Neon are negligible and not
# in GCP billing). Alert-only: threshold rules email billing admins at 80% and 100%; there is NO
# automatic billing hard-cap (explicitly out of scope). Defined here so the existing infra-plan / Trivy
# pipeline validates it like any other resource.

data "google_project" "this" {
  project_id = var.gcp_project_id
}

resource "google_billing_budget" "pr_envs" {
  billing_account = var.billing_account_id
  display_name    = "PR ephemeral environments"

  budget_filter {
    projects = ["projects/${data.google_project.this.number}"]
    # GCP budget label filter: match resources labelled pr-env=true (research D1/FR-014).
    labels = {
      "pr-env" = "true"
    }
  }

  amount {
    specified_amount {
      currency_code = "USD"
      units         = tostring(var.pr_env_monthly_budget)
    }
  }

  threshold_rules {
    threshold_percent = 0.8
  }
  threshold_rules {
    threshold_percent = 1.0
  }
}

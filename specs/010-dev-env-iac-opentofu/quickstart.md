# Quickstart: Provisioning the Dev Environment with OpenTofu

**Feature**: 010-dev-env-iac-opentofu

End-to-end operator runbook: from a clean account to a `009`-ready Dev environment. This mirrors what
`infra/README.md` will contain.

## Prerequisites (day −1, manual once — cannot be IaC'd)

- A **Google Cloud** project with billing enabled (`gcloud` installed, `gcloud auth application-default
  login` done).
- A **Cloudflare** account with the `nekohoa.com` zone, and an **API token** scoped to Pages, R2, and
  DNS edit for that zone; note the **Account ID** and **Zone ID**.
- A **Neon** account and an **API key**.
- OpenTofu ≥ 1.8 installed (`tofu version`).

## Step 1 — Bootstrap the state bucket (once)

```bash
cd infra/bootstrap/state-bucket
tofu init            # local state — no backend yet
tofu apply -var gcp_project_id=<PROJECT> -var bucket_name=<UNIQUE_STATE_BUCKET>
# Creates the versioned GCS state bucket and enables required GCP APIs.
```

Record the bucket name; it goes into each environment's `backend.tf`.

## Step 2 — Configure the Dev environment

```bash
cd ../../environments/dev
cp terraform.tfvars.example terraform.tfvars     # gitignored — never commit
$EDITOR terraform.tfvars                          # fill secrets/inputs (Neon key, CF token, etc.)
$EDITOR backend.tf                                # set bucket = <UNIQUE_STATE_BUCKET>, prefix = state/dev
```

`terraform.tfvars` supplies: `neon_api_key`, `cloudflare_api_token`, `cloudflare_account_id`,
`cloudflare_zone_id`, `gcp_project_id`, `gcp_region`, `github_repository`, the eight
`operator_secrets`, and `deploy_alert_webhook_url`. Leave `api_dns_proxied = false` for now.

## Step 3 — Plan & apply (cert step 1: grey-cloud)

```bash
tofu init            # configures the GCS backend
tofu plan -out tf.plan
tofu apply tf.plan
```

This creates Neon, Cloud Run `nekohoa-api-dev`, the SAs + WIF, the nine secrets (with
`dev-db-connection` auto-wired), Pages `nekohoa-dev`, the R2 bucket, and DNS with the API record
**unproxied** so Google can issue the custom-domain certificate.

## Step 4 — Flip the API domain to proxied (cert step 2)

Wait for the Cloud Run domain mapping to report the cert as active, then:

```bash
# set api_dns_proxied = true in terraform.tfvars
tofu apply
```

`api-dev.nekohoa.com` is now proxied through Cloudflare at Full(strict).

## Step 5 — Wire GitHub Actions (from outputs only)

```bash
tofu output                 # non-sensitive wiring values
tofu output cloudflare_api_token      # sensitive — view individually
```

Set the repository **secrets** `GCP_WIF_PROVIDER`, `GCP_DEPLOY_SERVICE_ACCOUNT`,
`CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `DEPLOY_ALERT_WEBHOOK_URL` and the **variable**
`GCP_REGION` (see `contracts/github-actions-wiring.md`).

## Step 6 — Enable the pipeline (LAST)

After verifying `https://api-dev.nekohoa.com/health` is healthy, set the repository variable
`DEV_DEPLOY_ENABLED=true`. The next merge to `main` triggers the `009` `deploy-dev` job, which pushes
the real image to `nekohoa-api-dev` (infra `ignore_changes` keeps it from being reverted).

## Day-2: making changes

- Edit HCL → open a PR → `infra-plan.yml` posts a plan (no changes applied).
- Merge → `infra-apply.yml` auto-applies **Dev**. (Prod, when added, pauses for required-reviewer
  approval via a protected GitHub Environment.)
- Re-running `tofu plan` on an unchanged env shows **no drift**.

## Validation checklist (maps to Success Criteria)

- [ ] No resource created by hand beyond Steps 1 prerequisites + bucket bootstrap (SC-001).
- [ ] `gcloud run services describe nekohoa-api-dev` matches matrix rows 1–6 + secret refs (SC-002).
- [ ] `tofu plan` after apply = no changes (SC-003).
- [ ] `dev-db-connection` = .NET keyword string from Neon, no `postgresql://` (SC-004).
- [ ] `git grep` finds no secret values; tfvars/state are gitignored (SC-005).
- [ ] GitHub wired from `tofu output` alone; `DEV_DEPLOY_ENABLED` set last (SC-006).
- [ ] PR plan changes nothing live; apply gated for Prod (SC-007).
- [ ] WIF impersonation rejected for any repo ≠ `nbon12/hoa_management_system` (SC-008).

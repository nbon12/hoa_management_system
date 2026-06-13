# Handoff: Infrastructure as Code (OpenTofu) — provision the Dev environment declaratively

**Status**: Not started — handoff from `009-dev-auto-deploy`.
**Decided**: tool = **OpenTofu**; delivery = **spec-kit feature** (run `/speckit.specify` with the prompt below).
**Why this exists**: `009-dev-auto-deploy` built the *pipeline* (the `deploy-dev` job) but left the
underlying cloud resources to be created by hand (its Phase 1, T001–T006). This feature replaces
that manual provisioning with declarative IaC so the whole Dev environment is reproducible from the
repo.

---

## Ready-to-use `/speckit.specify` prompt

> Paste the block below as the argument to `/speckit.specify` to generate the spec for this feature.

```
Create a declarative Infrastructure-as-Code setup, committed in the repo under infra/, using
OpenTofu, that provisions the entire Dev environment for the 009-dev-auto-deploy pipeline so no
cloud resources are created by hand. It must provision, via each provider's API:

- Neon (community provider kislerdm/neon): a Neon project, a Dev branch, a database, and a role,
  with the pooled connection endpoint exposed as an output.
- Google Cloud: the Cloud Run service "nekohoa-api-dev" (region from a variable, min-instances 0,
  allow-unauthenticated, container port 8080, ASPNETCORE_ENVIRONMENT=Dev, health probe on /health);
  a runtime service account with secretmanager.secretAccessor; a deployer service account with
  run.admin + iam.serviceAccountUser; Secret Manager secrets with the exact IDs the deploy job maps
  (dev-db-connection, dev-jwt-secret, dev-sentry-dsn, dev-stripe-secret-key, dev-stripe-webhook-secret,
  dev-storage-service-url, dev-storage-access-key, dev-storage-secret-key, dev-scheduler-secret),
  wiring the Neon connection-string output directly into dev-db-connection; and a Workload Identity
  Federation pool/provider that lets the GitHub repo impersonate the deployer service account.
- Cloudflare: a Pages project named "nekohoa-dev" (production branch main), the R2 Dev documents
  bucket, and the DNS records for dev.nekohoa.com (Pages) and api-dev.nekohoa.com (Cloud Run custom
  domain).

State is stored in a GCS backend bucket. Secrets (Neon password, Cloudflare API token, Neon API
key, GCP credentials) are supplied via a gitignored tfvars file and never committed. Outputs print
the GitHub Actions secret/variable values an operator must set (GCP_WIF_PROVIDER,
GCP_DEPLOY_SERVICE_ACCOUNT, CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID, GCP_REGION,
DEPLOY_ALERT_WEBHOOK_URL, and the reminder to set DEV_DEPLOY_ENABLED=true last). Provide a plan-only
GitHub Actions workflow on pull requests and a gated apply on merge. The design must extend cleanly
to Staging and Prod later (per Constitution §10).
```

---

## Context the spec/plan must honor (contract with 009)

The IaC **must** match the names hardcoded in `.github/workflows/test.yml` `deploy-dev`, else the
pipeline breaks. Source of truth: `specs/009-dev-auto-deploy/contracts/environment-matrix.md`.

| Resource | Exact name / value |
|----------|--------------------|
| Cloud Run service | `nekohoa-api-dev` |
| Cloud Run region | = GitHub variable `GCP_REGION` |
| Container port | `8080` (Dockerfile `EXPOSE 8080`) |
| Image (pulled, not built by TF) | `sakurapatch/nekohoa-api:<sha>` (Docker Hub) |
| Cloud Run env | `ASPNETCORE_ENVIRONMENT=Dev`, `--allow-unauthenticated` |
| Secret Manager IDs → env vars | `dev-db-connection`→`ConnectionStrings__DefaultConnection`, `dev-jwt-secret`→`Jwt__Secret`, `dev-sentry-dsn`→`Sentry__Dsn`, `dev-stripe-secret-key`, `dev-stripe-webhook-secret`, `dev-storage-service-url`, `dev-storage-access-key`, `dev-storage-secret-key`, `dev-scheduler-secret` |
| Neon connection string format | **Npgsql/.NET keyword format**, pooled endpoint, `SSL Mode=Require;Channel Binding=Require` (NOT the `postgresql://` URI) |
| Cloudflare Pages project | `nekohoa-dev`, production branch `main` |
| Frontend domain | `dev.nekohoa.com` (Pages custom domain) |
| API domain | `api-dev.nekohoa.com` (Cloud Run domain mapping; CNAME → `ghs.googlehosted.com`, grey-cloud for cert then proxied Full(strict)) |
| GitHub secrets the job reads | `GCP_WIF_PROVIDER`, `GCP_DEPLOY_SERVICE_ACCOUNT`, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `DEPLOY_ALERT_WEBHOOK_URL` |
| GitHub variables | `GCP_REGION`, `DEV_DEPLOY_ENABLED` (set `true` last) |

## Boundaries (cannot be IaC'd — day −1, manual once)

- GCP project + billing account; Cloudflare account; a Neon account + **API key**.
- An initial credential to run `tofu apply` (your `gcloud` ADC login + Cloudflare token + Neon API
  key) and the **GCS state bucket** (chicken-and-egg: create the state bucket manually or with a
  tiny local-state bootstrap step).

## Caveats / risks to call out in the spec

- The **Neon Terraform/OpenTofu provider is community-maintained** (`kislerdm/neon`), not
  HashiCorp/OpenTofu-verified. Pin its version.
- Cloud Run custom-domain mapping + Cloudflare proxy has a cert-issuance ordering gotcha (grey-cloud
  first). The DNS resource may need a two-step or a documented manual flip.
- WIF pool/provider + repo binding is the fiddliest part; get the attribute condition (repo owner)
  right so only this repo can impersonate the deployer SA.
- Never commit tokens/passwords (Constitution §8 / SC-006): tfvars gitignored, outputs marked
  `sensitive` where applicable.

## What this absorbs from 009

This feature delivers `009`'s deferred **Phase 1 (T001–T006)** provisioning as code, plus the WIF
setup. Once applied, the operator only sets the GitHub secrets/variables and flips
`DEV_DEPLOY_ENABLED=true`; the `009` pipeline does the rest.

# `infra/` — Declarative environment provisioning (OpenTofu)

<!-- REPOWISE:START section=infra-overview -->
This tree provisions, declaratively with **OpenTofu (≥ 1.8)**, the entire cloud environment that the
[`009-dev-auto-deploy`](../specs/009-dev-auto-deploy/) pipeline deploys into. Nothing it depends on is
created by hand. A single reusable module, [`modules/environment`](./modules/environment), defines one
environment; per-environment directories ([`environments/dev`](./environments/dev), and a
[`staging`](./environments/staging) skeleton) instantiate it with their own inputs and an isolated GCS
state prefix.

Per environment the module provisions:

- **Neon** — project / `dev` branch / database / role, and assembles the **pooled .NET keyword**
  connection string consumed as `ConnectionStrings__DefaultConnection`.
- **Google Cloud Run** (`nekohoa-api-${env_name}`, e.g. `nekohoa-api-dev`) — scale-to-zero, port
  8080, `ASPNETCORE_ENVIRONMENT` from input, `/health` probes, the 9 Secret Manager refs, a runtime
  service account, and `allUsers` invoker. The container image is **pipeline-owned** (`ignore_changes`).
- **Secret Manager** — the nine `dev-*` secrets the deploy job maps, with `dev-db-connection`
  auto-wired from Neon.
- **IAM / Workload Identity Federation** — runtime + deployer service accounts and a repo-scoped WIF
  pool/provider so this GitHub repo deploys without long-lived keys.
- **Cloudflare** — Pages project `nekohoa-${env_name}`, the R2 documents bucket, and the
  `dev.nekohoa.com` / `api-dev.nekohoa.com` DNS records.

The one-time **state bucket** is created by [`bootstrap/state-bucket`](./bootstrap/state-bucket)
(local state) to resolve the backend chicken-and-egg. Two workflows wrap the lifecycle:
`infra-plan.yml` (plan-only on PRs touching `infra/`) and `infra-apply.yml` (apply on merge; Prod
gated behind a protected GitHub Environment).

**Every resource name/value is pinned to the
[009 environment matrix](../specs/009-dev-auto-deploy/contracts/environment-matrix.md);** a mismatch
breaks the pipeline.
<!-- REPOWISE:END -->

## Operator runbook

The full step-by-step is in [`specs/010-dev-env-iac-opentofu/quickstart.md`](../specs/010-dev-env-iac-opentofu/quickstart.md).
In short:

1. **Bootstrap** the state bucket once — [`bootstrap/state-bucket`](./bootstrap/state-bucket).
2. **Configure** `environments/dev`: copy `terraform.tfvars.example` → `terraform.tfvars` (gitignored)
   and set `bucket`/`prefix` in `backend.tf`.
3. **Apply** twice for the cert flip: grey-cloud (`api_dns_proxied = false`) → wait for the Cloud Run
   domain-mapping cert → proxied (`api_dns_proxied = true`).
4. **Wire GitHub** from `tofu output` (see
   [`contracts/github-actions-wiring.md`](../specs/010-dev-env-iac-opentofu/contracts/github-actions-wiring.md)).
5. Set `DEV_DEPLOY_ENABLED=true` **last**.

## Layout

| Path | Purpose |
|------|---------|
| `bootstrap/state-bucket/` | One-time, local-state bootstrap of the versioned GCS state bucket + GCP API enablement. |
| `modules/environment/` | The single reusable environment definition (Neon + Cloud Run + IAM/WIF + Secrets + Cloudflare). |
| `environments/dev/` | Dev instantiation: backend prefix `state/dev`, tfvars, module block. |
| `environments/staging/` | Reuse skeleton (not applied) proving Staging/Prod need no module edits. |

> Never commit `*.tfvars` (except `*.example`), `*.tfstate*`, or `*.tfplan` — see `.gitignore`.

## CI/CD (`.github/workflows/`)

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| `infra-plan.yml` | PR touching `infra/**` | WIF auth → `fmt -check` / `init` / `validate` / `plan` for `environments/dev`, posts the plan to the PR. **No live changes** (SC-007). |
| `infra-apply.yml` | push to `main` touching `infra/**` | WIF auth → `apply` for `environments/dev` **automatically**. A commented, `environment: prod`-gated Prod job is included to enable when `environments/prod` exists (FR-026). |

### Required GitHub configuration

**Repository secrets** (consumed by both workflows): `GCP_WIF_PROVIDER`, `GCP_DEPLOY_SERVICE_ACCOUNT`,
`GCP_PROJECT_ID`, `NEON_API_KEY`, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`,
`CLOUDFLARE_ZONE_ID`, `DEPLOY_ALERT_WEBHOOK_URL`, and `TF_VAR_OPERATOR_SECRETS` (a **JSON object** of
the eight operator secret values, e.g. `{"jwt-secret":"…","sentry-dsn":"…",…}`). The first two come
straight from `tofu output` (`wif_provider`, `deployer_service_account`).

> These wire the **infra workflows** (which run OpenTofu). They are separate from the **009 deploy**
> secrets/variables an operator sets from `tofu output` (see
> [`contracts/github-actions-wiring.md`](../specs/010-dev-env-iac-opentofu/contracts/github-actions-wiring.md)).

### Protected `prod` Environment (T030)

Before enabling the Prod apply job, create a **protected GitHub Environment** so Prod pauses for human
approval (auto-apply Dev/Staging, gate Prod — FR-026):

1. Repo **Settings → Environments → New environment** → name it `prod`.
2. Enable **Required reviewers** and add the approver(s).
3. (Optional) Restrict deployment branches to `main`.
4. Add the `PROD_*` secrets referenced by the commented `apply-prod` job, then uncomment that job and
   point its `working-directory` at `infra/environments/prod`.

The `prod` Environment's required-reviewer gate is what makes `infra-apply.yml` stop for approval on
Prod while Dev applies straight through.

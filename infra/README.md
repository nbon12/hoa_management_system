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
- **Artifact Registry** (`images-${env_name}`) — the Docker repository CI pushes the backend image to
  (deployer SA as writer, via WIF) and Cloud Run pulls from. Per-PR `pr-*` tags are deleted after 14
  days. Cloud Run no longer pulls from Docker Hub: its `mirror.gcr.io` path rate-limits uncached tags.
- **Secret Manager** — the nine `dev-*` secrets the deploy job maps, with `dev-db-connection`
  auto-wired from Neon.
- **IAM / Workload Identity Federation** — runtime + deployer service accounts and a repo-scoped WIF
  pool/provider so this GitHub repo deploys without long-lived keys.
- **Cloudflare** — Pages project `nekohoa-${env_name}`, the R2 documents bucket, and the
  `dev.nekohoa.com` / `api-dev.nekohoa.com` DNS records.
- **Cloud Scheduler** (`nekohoa-arc-sweep-${env_name}`, spec 027) — an hourly `POST` to the API's
  `/api/v1/architectural/jobs/sweep`, authenticated by the `X-Scheduler-Secret` header. The header takes
  the operator `scheduler-secret` value from tfvars / `TF_VAR_OPERATOR_SECRETS`, so rotate it there and
  not out-of-band. PR environments get no job.

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
   and set `bucket`/`prefix` in `backend.tf`, plus the same bucket as the module's `state_bucket` input in
   `main.tf` (the deployer SA is granted `roles/storage.objectAdmin` on it so CI can read and write state).
3. **Apply** twice for the cert flip: grey-cloud (`api_dns_proxied = false`) → wait for the Cloud Run
   domain-mapping cert → proxied (`api_dns_proxied = true`).
4. **Wire GitHub** from `tofu output` (see
   [`contracts/github-actions-wiring.md`](../specs/010-dev-env-iac-opentofu/contracts/github-actions-wiring.md)).
5. Set `DEV_DEPLOY_ENABLED=true` **last**.
6. After the first deploy, create the first **Company Administrator** — see
   [First Company Administrator](#first-company-administrator-setup-step-spec-030).

## First Company Administrator (setup step, spec 030)

The first Company Administrator of a deployment is created by a one-time command-line step built
into the API image ([030 FR-038](../specs/030-company-administration/spec.md);
[contract](../specs/030-company-administration/contracts/company.md#setup-step-command-line-never-http-fr-038)).
It never runs through an HTTP endpoint and is allowed in every environment, because it is the
operator path. It applies pending migrations first.

```text
dotnet HOAManagementCompany.dll --grant-company-admin <email> [--company-name "<name>"]
dotnet HOAManagementCompany.dll --grant-company-admin <email> --recover
```

| Situation | What the step does |
|-----------|--------------------|
| No live Company Administrator grant | If `<email>` has an account, grants it the role (`Source = Setup`), logs `CompanyAdminChange Actor=setup Change=Granted` and prints `Granted Company Administrator to <userId>.` Otherwise emails a Company Administrator invitation (72 hours, single use, bound to that email) and prints `Invitation sent; it expires <time UTC>.` |
| A live grant exists, without `--recover` | Prints `A Company Administrator already exists; nothing changed.`, writes nothing and exits 0, so re-running it is safe. |
| A live grant exists, with `--recover` | For when every Company Administrator is unavailable or compromised. Grants or invites as above with `Source = Recovery`, logs `CompanyAdminChange Change=Recovery`, and emails every current Company Administrator (`co_admin_recovery`, queued through the outbox). |

`--company-name` sets the management company's name, which emails and the company sign-in page use.

| Exit code | Meaning |
|-----------|---------|
| `0` | Done, including "nothing changed". |
| `1` | Failure, e.g. a database error or the invitation email couldn't be sent. Fix the cause and run it again; a re-run resends the same invitation with a new link. |
| `2` | Bad arguments, e.g. a missing or invalid email or an empty `--company-name`. The reason is printed. |

**Deployed environments.** Run the step as a **Cloud Run job execution of the service image** with
these arguments. The image's entrypoint is `dotnet HOAManagementCompany.dll`, so the arguments pass
straight through. The job needs the same `ASPNETCORE_ENVIRONMENT`, Secret Manager references and
runtime service account as the environment's `nekohoa-api-${env_name}` service, because the step
connects to the database and sends email. This tree does **not** provision the job; create it once
per environment from the service's current image before executing it. Then:

```bash
gcloud run jobs execute <service>-admin-setup --region <region> \
  --args="--grant-company-admin,owner@example.com"

# Recovery mode
gcloud run jobs execute <service>-admin-setup --region <region> \
  --args="--grant-company-admin,owner@example.com,--recover"
```

What the step printed is in the execution's logs; a non-zero exit code fails the execution. Locally,
run `dotnet run --project HOAManagementCompany -- --grant-company-admin owner@example.com` (see
[030 quickstart](../specs/030-company-administration/quickstart.md#the-setup-step-first-company-administrator-fr-038)).

## Layout

| Path | Purpose |
|------|---------|
| `bootstrap/state-bucket/` | One-time, local-state bootstrap of the versioned GCS state bucket + GCP API enablement. |
| `modules/environment/` | The single reusable environment definition (Neon + Cloud Run + Artifact Registry + IAM/WIF + Secrets + Cloudflare + the ARC sweep Cloud Scheduler job). |
| `environments/dev/` | Dev instantiation: backend prefix `state/dev`, tfvars, module block, and the account-level PR-environment billing budget (`budget.tf`, 013 SC-008). |
| `environments/staging/` | Reuse skeleton (not applied) proving Staging/Prod need no module edits. |
| `modules/pr-environment/` | One PR's environment (013): Neon branch `pr-<n>` forked from `pr-base`, R2 bucket `nekohoa-pr-<n>-documents`, Cloud Run `nekohoa-api-pr-<n>`, and the `pr-<n>-db-connection` / `pr-<n>-stripe-webhook` secrets. Reuses Dev's GCP project, Neon project, runtime service account and operator secrets. |
| `environments/pr/` | Per-PR root (013): one root serves every PR, with state prefix `state/pr/<n>` supplied at `tofu init -backend-config`. |

> Never commit `*.tfvars` (except `*.example`), `*.tfstate*`, or `*.tfplan` — see `.gitignore`.

## CI/CD (`.github/workflows/`)

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| `infra-plan.yml` | PR touching `infra/**` | WIF auth → `fmt -check` / `init` / `validate` / `plan` for `environments/dev`, posts the plan to the PR. **No live changes** (SC-007). |
| `infra-apply.yml` | push to `main` touching `infra/**` | WIF auth → `apply` for `environments/dev` **automatically**. A commented, `environment: prod`-gated Prod job is included to enable when `environments/prod` exists (FR-026). |

Neither workflow plans or applies the per-PR root. `pr-env.yml` provisions it, `pr-env-teardown.yml`
destroys it when the PR closes, `pr-env-sweep.yml` reclaims orphaned or inactive ones daily (see
[013 contracts/workflows.md](../specs/013-ephemeral-pr-envs/contracts/workflows.md)), and
`pr-env-repair.yml` is the manually dispatched repair path (issue #234).

### Required GitHub configuration

**Repository secrets** (consumed by both workflows): `GCP_WIF_PROVIDER`, `GCP_DEPLOY_SERVICE_ACCOUNT`,
`GCP_PROJECT_ID`, `NEON_API_KEY`, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`,
`CLOUDFLARE_ZONE_ID`, `DEPLOY_ALERT_WEBHOOK_URL`, `TF_VAR_BILLING_ACCOUNT_ID` (the GCP billing account,
`XXXXXX-XXXXXX-XXXXXX`, that the 013 PR-environment budget in `environments/dev/budget.tf` is created
under), and `TF_VAR_OPERATOR_SECRETS` (a **JSON object** of
the eight operator secret values, e.g. `{"jwt-secret":"…","sentry-dsn":"…",…}`). The first two come
straight from `tofu output` (`wif_provider`, `deployer_service_account`).

**Repository variable** (consumed by both workflows): `STRIPE_PUBLISHABLE_KEY`, the non-secret
`pk_test_…` key. It is passed as `TF_VAR_stripe_publishable_key` and set on Cloud Run as
`Stripe__PublishableKey`; the API refuses to start without it.

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

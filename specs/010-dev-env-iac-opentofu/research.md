# Phase 0 Research: Declarative Dev Environment Provisioning (OpenTofu)

**Feature**: 010-dev-env-iac-opentofu | **Date**: 2026-06-13

All four clarification items were resolved in `/speckit.clarify` (see spec → Clarifications). This
document records the remaining technical decisions the plan depends on, each as
Decision / Rationale / Alternatives.

## 1. IaC tool & provider set

- **Decision**: OpenTofu ≥ 1.8 (Terraform-compatible HCL). Providers, all version-pinned in
  `modules/environment/versions.tf`: `hashicorp/google` + `hashicorp/google-beta` (Cloud Run v2,
  Secret Manager, IAM, WIF, GCS backend), `cloudflare/cloudflare`, and the **community**
  `kislerdm/neon`.
- **Rationale**: OpenTofu is the handoff-decided tool. `google-beta` is needed for some Cloud Run v2
  and WIF features. The GCS backend is a first-class OpenTofu backend.
- **Alternatives**: Pulumi/CDKTF (rejected — handoff fixed OpenTofu); Terraform CE (rejected —
  OpenTofu chosen for licensing).

## 2. Neon provider (community) — pin & connection-string shape

- **Decision**: Use `kislerdm/neon`, **pinned to an exact patch version** (e.g.
  `version = "= 0.6.3"`, confirmed at implementation against the registry). Create
  `neon_project` → `neon_branch` (name `dev`) → `neon_database` → `neon_role`. Build the
  `dev-db-connection` value in HCL from the **pooled** endpoint host and the role/password as a
  **.NET keyword string**: `Host=<pooled-host>;Database=<db>;Username=<role>;Password=<pwd>;SSL
  Mode=Require;Channel Binding=Require` — never the provider's `postgresql://` URI.
- **Rationale**: The provider exposes the pooled host (`-pooler` suffix) and credentials as
  attributes; assembling the keyword string in HCL (vs consuming a URI) is what the .NET app expects
  (FR-003) and avoids a lossy URI→keyword conversion. Exact pin mitigates the community-provider risk
  (FR-021).
- **Alternatives**: Emit the `postgresql://` URI (rejected — wrong format for Npgsql/.NET);
  HashiCorp-verified provider (none exists for Neon).
- **Risk noted**: community-maintained provider — pinned, and a `versions.tf` comment flags it.

## 3. Cloud Run service (v2) configuration

- **Decision**: `google_cloud_run_v2_service "nekohoa-api-dev"` with: `scaling.min_instance_count =
  0`; container `ports.container_port = 8080`; env `ASPNETCORE_ENVIRONMENT = "Dev"`; secret env vars
  via `env { value_source.secret_key_ref }` for the nine secrets mapped to their .NET keys; a startup
  **and** liveness probe on `GET /health`; `service_account` = the runtime SA. Public access via a
  separate `google_cloud_run_v2_service_iam_member` granting `roles/run.invoker` to `allUsers`
  (= `--allow-unauthenticated`). A `lifecycle { ignore_changes = [template[0].containers[0].image,
  client, client_version] }` so the 009 pipeline owns the image/revision (FR-007).
- **Rationale**: Matches the 009 matrix exactly (port, env, scale-to-zero, unauthenticated, health).
  `ignore_changes` on the image is the standard way to let a separate deploy pipeline manage
  revisions without an infra apply reverting them.
- **Alternatives**: Cloud Run v1 (`google_cloud_run_service`) — rejected, v2 is current and models
  probes/secret refs more cleanly. Managing the image in TF — rejected, violates FR-007.
- **Bootstrap image**: first-ever create uses a placeholder image var (default
  `sakurapatch/nekohoa-api:latest`); after the pipeline pushes a real `:sha`, `ignore_changes` keeps
  it.

## 4. Secret Manager — IDs and auto-wiring

- **Decision**: Nine `google_secret_manager_secret` resources with `secret_id` = the **exact** IDs
  (`dev-db-connection`, `dev-jwt-secret`, `dev-sentry-dsn`, `dev-stripe-secret-key`,
  `dev-stripe-webhook-secret`, `dev-storage-service-url`, `dev-storage-access-key`,
  `dev-storage-secret-key`, `dev-scheduler-secret`). `dev-db-connection` gets a
  `google_secret_manager_secret_version` whose payload is the Neon keyword string (FR-014). The other
  eight take their first version from gitignored tfvars values, with `lifecycle { ignore_changes =
  [secret_data] }` so operators/the app can rotate versions out-of-band without infra drift.
- **Rationale**: Auto-wiring the DB connection is the key ergonomic win (SC-004). `ignore_changes`
  on operator-rotated secrets prevents the apply from fighting manual rotations.
- **Alternatives**: Create empty secrets and have operators add versions by hand (rejected — defeats
  "no hand-created config"); store all secret bodies in tfvars (kept for the 8 operator secrets,
  which is unavoidable, but they are gitignored).

## 5. Service accounts & Workload Identity Federation

- **Decision**: Two SAs — **runtime** (`run.invoker` not needed; granted `roles/secretmanager.
  secretAccessor`, scoped to the nine secrets via per-secret IAM members) and **deployer** (project
  `roles/run.admin` + `roles/iam.serviceAccountUser` on the runtime SA). One
  `google_iam_workload_identity_pool` + `google_iam_workload_identity_pool_provider` (OIDC,
  issuer `https://token.actions.githubusercontent.com`) with attribute mapping
  `google.subject = assertion.sub`, `attribute.repository = assertion.repository`, and an
  **attribute condition** `assertion.repository == "nbon12/hoa_management_system"`. A
  `google_service_account_iam_member` binds `roles/iam.workloadIdentityUser` to
  `principalSet://…/attribute.repository/nbon12/hoa_management_system` on the deployer SA.
- **Rationale**: Repo-scoped, **not** ref-restricted (clarified) — so PR plan runs and merge applies
  both authenticate without a second identity (FR-012). Least privilege per §7.
- **Alternatives**: Ref/branch-restricted condition (rejected per clarification — would block PR
  plans); long-lived SA JSON keys (rejected — §8 / FR-027).
- **Risk noted**: attribute condition must be exact; a too-broad `assertion.repository_owner`
  condition would let any repo in the org impersonate — we pin the full `owner/repo`.

## 6. Cloudflare — Pages, R2, DNS, and the cert-ordering gotcha

- **Decision**: `cloudflare_pages_project "nekohoa-dev"` with `production_branch = "main"`;
  `cloudflare_r2_bucket` for Dev documents; `cloudflare_record` for `dev.nekohoa.com` (CNAME → Pages,
  proxied) and `api-dev.nekohoa.com` (CNAME → `ghs.googlehosted.com`). For the API record, a variable
  `api_dns_proxied` (default **false**) controls the orange/grey cloud. **Two-step**: apply once with
  `proxied = false` (grey-cloud) so Google issues the managed cert for the Cloud Run domain mapping;
  then set `api_dns_proxied = true` and re-apply for proxied Full(strict). The Cloud Run domain
  mapping itself is a `google_cloud_run_domain_mapping` for `api-dev.nekohoa.com`. The ordering and
  the flip are documented in `quickstart.md` (FR-019).
- **Rationale**: Cloudflare proxy in front of an un-issued Google-managed cert breaks ACME/cert
  issuance; grey-cloud first is the documented workaround. A variable makes the flip a one-line,
  reviewable change rather than a console action.
- **Alternatives**: Single proxied apply (rejected — cert issuance fails); fully manual DNS (rejected
  — defeats IaC). `cloudflare_pages_domain` resource is used to attach `dev.nekohoa.com` to Pages.

## 7. State backend & bootstrap (chicken-and-egg)

- **Decision**: One **versioned** GCS bucket (uniform bucket-level access, versioning on), with each
  environment's `backend "gcs"` using `prefix = "state/<env>"` (clarified). The bucket is created by
  `infra/bootstrap/state-bucket` which uses **local state** (no backend block) and is run once with
  operator ADC. The bootstrap also enables the required GCP APIs (`run`, `secretmanager`, `iam`,
  `iamcredentials`, `sts`, `storage`).
- **Rationale**: Single bucket is simpler to bootstrap and IAM, and per-prefix gives full state-object
  isolation (FR-020). Local-state bootstrap is the standard resolution to the backend chicken-and-egg
  (FR-031).
- **Alternatives**: Bucket-per-env (rejected per clarification — more to bootstrap); committing state
  (rejected — secrets + concurrency hazard).

## 8. CI/CD workflows

- **Decision**: `infra-plan.yml` triggers on `pull_request` with `paths: [infra/**]`, authenticates
  via `google-github-actions/auth` (WIF, `workload_identity_provider` + `service_account`), runs
  `tofu init/fmt -check/validate/plan` for `environments/dev`, and posts the plan summary to the PR.
  `infra-apply.yml` triggers on `push` to `main` with `paths: [infra/**]`; the Dev apply job runs
  automatically; a Prod apply job (added when Prod exists) targets a protected GitHub **Environment**
  (`prod`) with a required reviewer so it pauses for approval (clarified, FR-026). Cloudflare/Neon
  credentials come from GitHub **secrets** consumed as `TF_VAR_*`; no secret is committed.
- **Rationale**: Path-filtered, WIF-authenticated, environment-gated — matches FR-025/FR-026/FR-027
  and the clarified per-env gate policy.
- **Alternatives**: Apply everything behind manual approval (rejected per clarification for
  Dev/Staging); auto-apply Prod (rejected — Prod must be gated).

## 9. Secrets handling & outputs

- **Decision**: A committed `environments/dev/terraform.tfvars.example` documents every variable
  (placeholders only); `infra/.gitignore` excludes `*.tfvars` (except `*.example`), `*.tfstate*`,
  and `.terraform/`. Sensitive outputs (`db_connection_string`, any token echoes) are marked
  `sensitive = true`. Non-sensitive outputs print the GH wiring values (`GCP_WIF_PROVIDER`,
  `GCP_DEPLOY_SERVICE_ACCOUNT`, `GCP_REGION`, `CLOUDFLARE_ACCOUNT_ID`) plus a `next_steps` string that
  lists what to set and ends with "set `DEV_DEPLOY_ENABLED=true` **last**". `CLOUDFLARE_API_TOKEN` and
  `DEPLOY_ALERT_WEBHOOK_URL` are echoed back as **sensitive** outputs (they are operator inputs, not
  generated).
- **Rationale**: Satisfies FR-022/FR-023/FR-024 and SC-005/SC-006 — the operator wires GitHub from
  outputs alone, with no secret in plaintext or in the repo.
- **Alternatives**: Have TF write the GitHub secrets via the GitHub provider (rejected — keeps the
  repo free of any mechanism that can write its own CI secrets; matches the spec assumption).

## Open items deferred to implementation (not blocking)

- Exact pinned versions for each provider (confirm against registries at implementation).
- Neon compute/autoscaling settings for Dev (use provider defaults / scale-to-zero where available).
- R2 bucket location hint and Pages build config defaults (use sensible defaults; the 009 frontend
  build config already exists).

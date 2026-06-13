# Implementation Plan: Infrastructure as Code — Declarative Dev Environment Provisioning

**Branch**: `010-dev-env-iac-opentofu` | **Date**: 2026-06-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-dev-env-iac-opentofu/spec.md`

## Summary

Provision the **entire Dev environment** that the `009-dev-auto-deploy` pipeline deploys into,
declaratively with **OpenTofu**, committed under `infra/`, so no cloud resource is created by hand.
The configuration provisions, via each provider's API: a **Neon** project/Dev-branch/database/role
(exposing the pooled connection string in .NET keyword format); a Google **Cloud Run** service
`nekohoa-api-dev` plus a runtime service account (secret accessor), a deployer service account
(run.admin + iam.serviceAccountUser), the nine **Secret Manager** entries the deploy job maps (with
`dev-db-connection` auto-wired from the Neon output), and a **Workload Identity Federation**
pool/provider that lets this GitHub repo impersonate the deployer SA; and **Cloudflare** Pages
project `nekohoa-dev`, the R2 Dev bucket, and the DNS records for `dev.nekohoa.com` and
`api-dev.nekohoa.com`.

The technical approach is a **reusable shared module** (`infra/modules/environment`) consumed by
**per-environment directories** (`infra/environments/dev`, later `staging`/`prod`), each with its own
tfvars and a **GCS backend** keyed by a per-environment prefix in one versioned state bucket. The
state bucket itself is created by a tiny **local-state bootstrap** (`infra/bootstrap/state-bucket`)
to resolve the chicken-and-egg. Two GitHub Actions workflows wrap the lifecycle: **plan-only on PRs**
touching `infra/`, and **apply on merge** — auto-applying Dev/Staging, but gating **Prod** behind a
protected GitHub Environment with a required reviewer. All credentials are supplied via a
**gitignored tfvars file** (a committed `*.example` documents the shape); CI authenticates via the
WIF provider, never long-lived keys. Outputs print the exact GitHub Actions secret/variable values an
operator must set, ending with the instruction to flip `DEV_DEPLOY_ENABLED=true` **last**. Every
resource name/value is pinned to `specs/009-dev-auto-deploy/contracts/environment-matrix.md`.

## Technical Context

**Language/Version**: HCL for **OpenTofu** ≥ 1.8 (Terraform-compatible); GitHub Actions YAML + Bash
for the wrapper workflows
**Primary Dependencies**: Providers (versions pinned in `versions.tf`) — `hashicorp/google` &
`hashicorp/google-beta` (Cloud Run v2, Secret Manager, IAM, WIF, GCS), `cloudflare/cloudflare`
(Pages, R2, DNS), and the **community** `kislerdm/neon` provider (project/branch/database/role,
pooled endpoint). `tofu` CLI (`fmt`, `validate`, `plan`, `apply`); `gcloud` ADC for the bootstrap.
**Storage**: Remote state in a **single versioned GCS bucket**, per-environment prefix
(`state/dev`, `state/staging`, `state/prod`). Provisions (does not store app data in): Neon Dev
PostgreSQL, Cloudflare R2 Dev bucket, GCP Secret Manager.
**Testing**: `tofu fmt -check`, `tofu validate`, `tofu plan` (no-diff/idempotency check), provider
schema validation, and a name-by-name conformance check against the 009 environment matrix. No
xUnit/Jasmine — this feature ships no application code.
**Target Platform**: Google Cloud (Cloud Run, IAM, Secret Manager, GCS), Cloudflare (Pages, R2,
DNS), Neon — all provisioned from operator workstation or GitHub Actions runners (Linux).
**Project Type**: Infrastructure as Code (new top-level `infra/` tree) + CI/CD workflows. No
backend/frontend source changes.
**Performance Goals**: A full `apply` of a clean Dev environment completes in a single operator
session (minutes, dominated by Cloud Run + cert provisioning); a re-`plan` on an unchanged
environment reports **zero drift** (SC-003).
**Constraints**: Exact-name fidelity to the 009 contract (FR-029/SC-002); no secrets in repo or
non-sensitive output (SC-005); WIF repo-scoped, no long-lived keys (FR-011/FR-027); apply must not
clobber the pipeline-managed Cloud Run revision/image (FR-007); only documented manual steps are the
day-(-1) accounts and the one-time state-bucket bootstrap (FR-031/SC-001).
**Scale/Scope**: One environment delivered (Dev); structure parameterized for 3 (Dev/Staging/Prod).
~1 shared module (Neon + Cloud Run + IAM/WIF + Secrets + Cloudflare), 1 env directory, 1 bootstrap,
2 workflows, plus `.example` tfvars, `.gitignore`, and an operator runbook.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ Provisions exactly the constitution-mandated platforms — Neon PostgreSQL,
  Cloud Run (scale-to-zero), Cloudflare (Pages + R2 + edge in front of the API), Secret Manager,
  GitHub Actions. OpenTofu is the project-decided IaC tool (handoff). No new runtime tech is added;
  Auth0/FastEndpoints/Swashbuckle/Sentry are **not applicable** to a provisioning feature beyond
  wiring the `dev-sentry-dsn` secret slot.
- **HOA tenancy**: ⛔ Not applicable — no HOA-scoped data rows or queries. The relevant boundary is
  **environment isolation** (separate Neon DB, Cloud Run service, and state per env), satisfied by
  the per-environment module instantiation (§10, §3).
- **API contracts**: ⛔ Not applicable — no application endpoints added. The feature's "contracts"
  are the GitHub Actions wiring contract and the 009 environment-matrix fidelity contract (see
  `contracts/`).
- **Security and operations**: ✅ Secrets externalized to gitignored tfvars + Secret Manager;
  least-privilege service accounts; repo-scoped WIF instead of static keys; sensitive outputs marked
  `sensitive`; no secret values in repo or image layers (§7, §8). Serilog/Sentry are runtime concerns
  owned by the app; this feature provisions their secret slots.
- **File storage**: ✅ Provisions the Cloudflare **R2** Dev documents bucket for the hosted env;
  MinIO remains the local/test substitute and is untouched; no binary payloads handled here (§8).
- **Caching/edge**: ✅ Edge sits in front of the API via the proxied `api-dev.nekohoa.com` custom
  domain (grey-cloud during cert issuance, then proxied Full(strict)); no app-response caching is
  introduced (§2, §8).
- **Testing discipline**: ✅ Adapted to IaC — `fmt`/`validate`/`plan` gates, idempotent re-plan,
  and matrix conformance replace unit/integration suites (no application code ships). The 95%
  coverage gate targets app code and is N/A here (documented in spec → Constitution Requirements).
- **CI/CD and documentation**: ✅ Plan-on-PR + gated apply-on-merge via GitHub Actions; environment
  isolation per §10; Repowise marker regions refreshed for the PR. Sonar/Codecov operate on app code
  and are unaffected by HCL-only changes.

**Result**: PASS — no violations; Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/010-dev-env-iac-opentofu/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output — provider/WIF/cert/state decisions
├── data-model.md        # Phase 1 output — resource inventory + module variables/outputs
├── quickstart.md        # Phase 1 output — operator runbook (bootstrap → apply → wire GH)
├── contracts/           # Phase 1 output
│   ├── github-actions-wiring.md      # outputs → GH secrets/variables the 009 job reads
│   ├── module-interface.md           # environment module inputs/outputs
│   └── matrix-conformance.md         # name-by-name check vs 009 environment-matrix
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
infra/
├── README.md                         # entry-point runbook (mirrors quickstart.md)
├── .gitignore                        # *.tfvars (except *.example), *.tfstate*, .terraform/
├── bootstrap/
│   └── state-bucket/                 # local-state bootstrap for the GCS state bucket (FR-031)
│       ├── main.tf
│       ├── variables.tf
│       └── README.md
├── modules/
│   └── environment/                  # reusable per-environment module (FR-030)
│       ├── versions.tf               # required_providers with PINNED versions (FR-021)
│       ├── variables.tf              # env_name, region, domains, repo, image, etc.
│       ├── neon.tf                   # project, dev branch, database, role, pooled conn output
│       ├── cloud_run.tf              # nekohoa-api-dev service (port 8080, Dev, scale-to-zero)
│       ├── iam.tf                    # runtime SA, deployer SA, WIF pool/provider + bindings
│       ├── secrets.tf                # 9 Secret Manager entries; dev-db-connection auto-wired
│       ├── cloudflare.tf             # Pages nekohoa-dev, R2 bucket, DNS records
│       └── outputs.tf                # connection string, WIF provider, SA emails, GH wiring
└── environments/
    └── dev/
        ├── backend.tf                # GCS backend, prefix = state/dev
        ├── main.tf                   # module "environment" { env_name = "dev" ... }
        ├── variables.tf
        ├── outputs.tf                # re-exports module outputs (GH wiring values)
        └── terraform.tfvars.example  # non-secret template (FR-022)

.github/workflows/
├── infra-plan.yml                    # plan-only on PRs touching infra/ (FR-025)
└── infra-apply.yml                   # apply on merge; Prod gated by Environment (FR-026)
```

**Structure Decision**: A **shared `environment` module + per-environment directory** layout
(clarified 2026-06-13) gives each environment its own GCS-backend prefix and tfvars while reusing one
definition — directly satisfying FR-030/SC-009 and the constitution's isolation rule (§10). The
state bucket is bootstrapped separately with local state (FR-031). Workflows live in the repo's
existing `.github/workflows/` alongside the 009 pipeline.

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `infra/README.md` | `section=infra-overview` | What `infra/` provisions and how it maps to the 009 pipeline |
| `infra/modules/environment/outputs.tf` | `section=env-module-outputs` | The module's published outputs (connection string, WIF, GH wiring) |

### Marker syntax

```markdown
<!-- REPOWISE:START section=infra-overview -->
... generated content ...
<!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> No constitution violations — section intentionally empty.

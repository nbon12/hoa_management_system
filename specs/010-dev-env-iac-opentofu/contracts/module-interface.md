# Contract: `environment` Module Interface

**Feature**: 010-dev-env-iac-opentofu

The reusable module `infra/modules/environment` is the single definition of an environment. Each
environment directory (`infra/environments/<env>`) instantiates it with its own inputs and backend
prefix (FR-030, SC-009). This contract fixes the module boundary so Staging/Prod reuse it without
edits.

## Required inputs

See `data-model.md` → Module interface. Contract rules:

- **No environment name is hardcoded** inside the module; all naming derives from `var.env_name`
  (e.g. service `nekohoa-api-${env_name}` — for Dev this resolves to the contracted `nekohoa-api-dev`).
- **`ASPNETCORE_ENVIRONMENT` is a distinct input** (`var.aspnet_environment`), **not** derived
  verbatim from `env_name`: it maps `dev → Dev`, `staging → Staging`, `prod → Production`. The
  module MUST NOT assume `title(env_name)` (that would wrongly yield `Prod`). Dev passes `"Dev"`.
- Secret IDs are an exception: they are **literal** per the 009 contract (`dev-*`). For Staging/Prod
  the module takes a `secret_prefix` input (default derives from `env_name`) so the literal-vs-derived
  boundary is explicit; Dev passes `secret_prefix = "dev"` to match rows 8–16 of matrix-conformance.
- All secret-bearing inputs are typed `sensitive = true`.
- `github_repository` is required and used verbatim in the WIF attribute condition.

## Guaranteed outputs

The module MUST expose every output in `data-model.md` → Outputs. Environment directories re-export
them unchanged. `next_steps` MUST end with the instruction to set `DEV_DEPLOY_ENABLED=true` last.

## Backend contract (per environment directory)

```hcl
terraform {
  backend "gcs" {
    bucket = "<state-bucket-from-bootstrap>"
    prefix = "state/dev"   # state/staging, state/prod for the others
  }
}
```

## Provider version pinning (in module `versions.tf`)

```hcl
terraform {
  required_version = ">= 1.8.0"
  required_providers {
    google      = { source = "hashicorp/google",      version = "~> 5.0" }
    google-beta = { source = "hashicorp/google-beta",  version = "~> 5.0" }
    cloudflare  = { source = "cloudflare/cloudflare",  version = "~> 4.0" }
    # COMMUNITY-MAINTAINED — not HashiCorp/OpenTofu-verified. Pin exactly. (FR-021)
    neon        = { source = "kislerdm/neon",          version = "= 0.6.3" }
  }
}
```

(Exact versions confirmed against the registries at implementation time.)

## Reuse acceptance (SC-009)

Adding Staging/Prod requires only: a new `infra/environments/<env>/` dir (backend prefix + tfvars +
a `module "environment"` block with that env's inputs) — **zero** edits to `modules/environment`.

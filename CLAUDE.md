# HOAManagementCompany Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-06-14

## Active Technologies
- C# / .NET 9.0 (backend); TypeScript / Angular 17+ (frontend) (006-stripe-payments)
- PostgreSQL (Neon in production, Testcontainers in CI/local). New tables: `PaymentTransactions`, `ProcessedWebhookEvents`. Modified tables: `Owners` (+Stripe customer ID, alert opt-in flags, alert phone), `RecurringPayments` (vaulted PM reference + mandate fields; drop raw/masked card/bank fields). (006-stripe-payments)
- C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + Backend — FastEndpoints, EF Core 9 (Npgsql), **Stripe.net**, (006-stripe-payments)
- PostgreSQL — Neon in production, Testcontainers in CI/local. New tables: (006-stripe-payments)
- C# / .NET 9.0 + Stripe.net, SendGrid SDK, Twilio SDK (all already referenced by the backend); xUnit, Testcontainers.PostgreSQL, Microsoft.AspNetCore.Mvc.Testing (test project) (007-integration-ci-tests)
- PostgreSQL via Testcontainers for the webhook→persistence path (`WebhookEventInbox`); no schema changes (007-integration-ci-tests)
- C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + FastEndpoints (bundles **FluentValidation** — already used for (008-config-validation)
- N/A — no schema, migration, or persistence changes. (008-config-validation)
- C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend); GitHub + FastEndpoints, EF Core 9 (Npgsql), Serilog, Sentry; Angular CLI; (009-dev-auto-deploy)
- PostgreSQL — isolated **Neon Dev** database (separate from Staging/Prod); Cloudflare (009-dev-auto-deploy)
- HCL for **OpenTofu** ≥ 1.8 (Terraform-compatible); GitHub Actions YAML + Bash + Providers (versions pinned in `versions.tf`) — `hashicorp/google` & (010-dev-env-iac-opentofu)
- Remote state in a **single versioned GCS bucket**, per-environment prefix (010-dev-env-iac-opentofu)
- YAML (GitHub Actions workflow syntax); Trivy CLI (via `aquasecurity/trivy-action`); OpenTofu/HCL is the *scanned* artifact, not authored here + `aquasecurity/trivy-action`, `github/codeql-action/upload-sarif`, `docker/build-push-action`, `docker/setup-buildx-action`, `docker/login-action`, `actions/checkout` — all SHA-pinned (011-trivy-security-scanning)
- N/A (no application data, schema, or migrations) (011-trivy-security-scanning)

- C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) (005-otel-aspire-observability)

## Project Structure

```text
HOAManagementCompany/            # C# / .NET 9.0 backend (REST API, domain, EF Core persistence)
HOAManagementCompany.Tests/      # xUnit integration + performance tests (Testcontainers.PostgreSQL)
HOAManagementCompany.sln         # Solution file
neko-hoa/                        # Angular 17.3 frontend (single-page app, Storybook, Cypress/Playwright e2e)
specs/                           # Spec Kit feature specs (001-… through 009-…)
scripts/                         # Build/CI helper scripts
repowise/                        # Repowise index/config
wireframes/                      # UI wireframes
```

Note: there is no root `package.json`. Frontend npm commands run from `neko-hoa/`.

## Commands

Backend (run from repo root):
- `dotnet build`
- `dotnet test`                          # full xUnit suite (HOAManagementCompany.Tests)

Frontend (run from `neko-hoa/`):
- `npm ci`                               # install deps
- `npm run test:ci`                      # headless Karma unit tests (CI-safe)
- `npm run build`                        # production build
- `npm run e2e:ci`                       # Cypress e2e against a dev server

There is no `lint` npm script; do not run `npm run lint`.

## Code Style

C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend): Follow standard conventions

## Recent Changes
- 011-trivy-security-scanning: Added YAML (GitHub Actions workflow syntax); Trivy CLI (via `aquasecurity/trivy-action`); OpenTofu/HCL is the *scanned* artifact, not authored here + `aquasecurity/trivy-action`, `github/codeql-action/upload-sarif`, `docker/build-push-action`, `docker/setup-buildx-action`, `docker/login-action`, `actions/checkout` — all SHA-pinned
- 010-dev-env-iac-opentofu: Added HCL for **OpenTofu** ≥ 1.8 (Terraform-compatible); GitHub Actions YAML + Bash + Providers (versions pinned in `versions.tf`) — `hashicorp/google` &
- 008-config-validation: Added C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + FastEndpoints (bundles **FluentValidation** — already used for
- 009-dev-auto-deploy: Added C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend); GitHub + FastEndpoints, EF Core 9 (Npgsql), Serilog, Sentry; Angular CLI;


<!-- MANUAL ADDITIONS START -->

## Spec Kit workflow: PR watching

Spec Kit commands other than `/speckit.implement` (i.e. `/speckit.specify`,
`/speckit.clarify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.analyze`,
`/speckit.checklist`, `/speckit.constitution`, `/speckit.taskstoissues`) only produce
Markdown/spec artifacts under `specs/`. These changes do not exercise application code, so:

- Do **not** subscribe to PR activity, watch CI, or schedule self check-ins for PRs that
  contain only Spec Kit / Markdown document changes.
- Do **not** treat CI runs triggered by such doc-only PRs as something to babysit or autofix.
- Still commit and push the artifacts and open/update the draft PR as usual.

PR watching / CI babysitting is appropriate for `/speckit.implement` and other changes that
touch application or infrastructure code — unless the user explicitly asks otherwise.

## Spec Kit workflow: use the Spec Kit feature branch

`/speckit.specify` creates a numbered feature branch (e.g. `010-dev-env-iac-opentofu`) and the
matching `specs/<branch>/` directory. **That Spec Kit feature branch is the branch to use** for
all of the feature's spec-kit work and its PR.

- Do **not** move the work onto, or push it to, any `claude/*` branch that the harness may create
  by default (this happens especially in cloud sessions). Prefer the Spec Kit feature branch even
  when a `claude/*` development branch is designated for the session.
- If the session starts on a `claude/*` branch, switch to (or create) the Spec Kit feature branch
  reported by `/speckit.specify` and commit/push the spec-kit artifacts and PR there.
- The Spec Kit scripts also key off the branch name; running them from the feature branch keeps
  `check-prerequisites.sh` and friends working without needing `SPECIFY_FEATURE` overrides.

## Git Branch Lifecycle

After merging a branch into `main`, that branch is **immutable and closed**. Do not:
- Commit to it
- Push to it
- Check it out to continue work

If more work is needed after a merge:
1. Switch to `main`
2. Pull the latest changes: `git pull origin main`
3. Create a new branch off of `main`: `git checkout -b <new-branch-name>`
4. Do all further work on the new branch

Merged branches exist only as historical artifacts. Treat them as read-only.

<!-- MANUAL ADDITIONS END -->

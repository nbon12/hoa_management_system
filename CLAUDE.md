# HOAManagementCompany Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-06-13

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

- C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) (005-otel-aspire-observability)

## Project Structure

```text
src/
tests/
```

## Commands

npm test && npm run lint

## Code Style

C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend): Follow standard conventions

## Recent Changes
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

<!-- MANUAL ADDITIONS END -->

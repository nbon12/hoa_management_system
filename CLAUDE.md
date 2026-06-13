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
- 008-config-validation: Added C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + FastEndpoints (bundles **FluentValidation** — already used for
- 009-dev-auto-deploy: Added C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend); GitHub + FastEndpoints, EF Core 9 (Npgsql), Serilog, Sentry; Angular CLI;
- 007-integration-ci-tests: Added C# / .NET 9.0 + Stripe.net, SendGrid SDK, Twilio SDK (all already referenced by the backend); xUnit, Testcontainers.PostgreSQL, Microsoft.AspNetCore.Mvc.Testing (test project)
- 006-stripe-payments: Added C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + Backend — FastEndpoints, EF Core 9 (Npgsql), **Stripe.net**,


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

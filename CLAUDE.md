# HOAManagementCompany Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-06-11

## Active Technologies
- C# / .NET 9.0 (backend); TypeScript / Angular 17+ (frontend) (006-stripe-payments)
- PostgreSQL (Neon in production, Testcontainers in CI/local). New tables: `PaymentTransactions`, `ProcessedWebhookEvents`. Modified tables: `Owners` (+Stripe customer ID, alert opt-in flags, alert phone), `RecurringPayments` (vaulted PM reference + mandate fields; drop raw/masked card/bank fields). (006-stripe-payments)
- C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + Backend — FastEndpoints, EF Core 9 (Npgsql), **Stripe.net**, (006-stripe-payments)
- PostgreSQL — Neon in production, Testcontainers in CI/local. New tables: (006-stripe-payments)
- C# / .NET 9.0 + Stripe.net, SendGrid SDK, Twilio SDK (all already referenced by the backend); xUnit, Testcontainers.PostgreSQL, Microsoft.AspNetCore.Mvc.Testing (test project) (007-integration-ci-tests)
- PostgreSQL via Testcontainers for the webhook→persistence path (`WebhookEventInbox`); no schema changes (007-integration-ci-tests)

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
- 007-integration-ci-tests: Added C# / .NET 9.0 + Stripe.net, SendGrid SDK, Twilio SDK (all already referenced by the backend); xUnit, Testcontainers.PostgreSQL, Microsoft.AspNetCore.Mvc.Testing (test project)
- 006-stripe-payments: Added C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend) + Backend — FastEndpoints, EF Core 9 (Npgsql), **Stripe.net**,
- 006-stripe-payments: Added C# / .NET 9.0 (backend); TypeScript / Angular 17+ (frontend)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

# Implementation Plan: Stripe Payments (One-Time & Recurring)

**Branch**: `006-stripe-payments` | **Date**: 2026-06-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/006-stripe-payments/spec.md`

## Summary

Replace the NekoHOA resident portal's simulated/raw card collection with PCI-compliant
Stripe-hosted payment fields for both one-time payments (Payment Element) and recurring
auto-pay (SetupIntent + vaulted method-on-file), backed by a Stripe-specific transaction
audit trail, an append-only accounting ledger with statutory payment allocation, lifecycle
webhooks (success/failure/refund/partial-refund/dispute-create+resolve/ACH-return),
opt-in SMS/email failure alerts, and PII-scrubbed OpenTelemetry/Sentry telemetry.

The expanded spec adds money-correctness, compliance, and recovery requirements beyond a
basic Stripe integration: deterministic append-only ledger balances, refund/dispute/ACH-
return reversals (incl. partial refunds and NSF fees), category-priority payment allocation,
overpayment credit balances, durable webhook intake + reconciliation sweep + transactional
outbox, idempotent payment initiation, NACHA mandate capture/variable-amount notice, TCPA
consent capture, and North-Carolina-aware fee/late-fee configuration.

**Architectural keystone**: the backend runs on **Cloud Run scale-to-zero**, so the
scheduled draft job and the reconciliation sweep cannot be in-process timers — they are
**external Cloud Scheduler triggers** hitting authenticated internal endpoints.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend)
**Primary Dependencies**: Backend — FastEndpoints, EF Core 9 (Npgsql), **Stripe.net**,
**Twilio**, **SendGrid**, OpenTelemetry, Serilog, Sentry, FluentValidation. Frontend —
**@stripe/stripe-js** + **ngx-stripe**, RxJS, Angular standalone components.
**Storage**: PostgreSQL — Neon in production, Testcontainers in CI/local. New tables:
`PaymentTransactions`, `PaymentAuthorizations`, `AlertConsents`, `WebhookEventInbox`,
`OutboxMessages`, `HoaPaymentConfigs`, `Receipts`. Modified: `Owners`, `RecurringPayments`,
`LedgerEntry` (+sequence/timestamp ordering, new entry types, transaction FK), `DraftEntry`
(+transaction FK).
**Testing**: Backend — xUnit + .NET Testcontainers (PostgreSQL), transaction-per-test
isolation, Stripe/Twilio/SendGrid behind mockable gateway interfaces. Frontend — Jasmine/
Karma + Angular Testing Library, Playwright, Cypress (E2E), Storybook (visual).
**Target Platform**: Backend — Docker → Google Cloud Run (scale-to-zero, cold start
acceptable); Frontend — Cloudflare Pages; Cloudflare in front of the API.
**Project Type**: Web application (Angular SPA `neko-hoa/` + .NET API `HOAManagementCompany/`).
**Performance Goals**: One-time payment completable < 2 min (SC-002); recurring-failure
alert delivered < 5 min (SC-006); webhook acknowledged within Stripe's ~30s timeout
(durable-capture-then-process, FR-032).
**Constraints**: PCI **SAQ A** — no raw PAN/bank data ever reaches or is stored by the
backend (SC-001); PII-free telemetry (SC-007); Neon → low max connections + pooling +
short-lived DbContext; scale-to-zero → no in-process schedulers; append-only ledger with
deterministic balance recomputation (SC-009); exactly-one transaction per attempt (SC-003).
**Scale/Scope**: Per-HOA residents, modest TPS. ~14 endpoints (incl. 2 internal job
endpoints + webhook), ~7 new entities + 4 modified, 1 EF migration, Angular payment +
auto-pay + statement surfaces.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ FastEndpoints, .NET 9 REST, PostgreSQL/Neon, Docker→Cloud Run,
  Cloudflare, GitHub Actions all used as mandated. Stripe/Twilio/SendGrid are new external
  dependencies introduced by this feature (documented). Swashbuckle/FastEndpoints Swagger
  remains dev-only. **Auth note (CRITICAL constitution deviation — DEFERRED)**: the codebase
  currently uses local JWT/Identity, not the constitution-mandated Auth0 (§2/§7). This is a
  pre-existing, whole-codebase condition that this feature does **not** introduce and does
  **not** resolve — payment endpoints reuse the existing JWT `propertyId`-claim scoping. Per an
  explicit decision, the Auth0 migration is **out of scope for this feature branch and will be
  addressed in an upcoming dedicated feature branch**. Flagged by `/speckit.analyze` (C1);
  tracked, not diluted.
- **HOA tenancy**: ✅ Every new row is property/owner-scoped (the existing tenant boundary);
  webhook-driven updates resolve the local record by Stripe reference and stay within its
  scope (FR Constitution: Tenant boundary). Cross-HOA access denied by default.
- **API contracts**: ✅ New endpoints reuse the existing response/error envelope
  (`{code,message,errors}`), `limit`/`offset` pagination for collections, UTC timestamps,
  and document auth/cacheability (see `contracts/payments-api.md`). Webhook is the documented
  signature-authenticated exception.
- **Security and operations**: ✅ Secrets externalized via config (Stripe/Twilio/SendGrid
  keys, webhook secret); server-side authorization; Serilog structured logs + Sentry +
  OpenTelemetry already wired; production errors do not leak details (global handler).
  Adds rate limiting on payment endpoints (existing `payments` policy) + processor fraud
  tooling (FR-028), PII encryption at rest (FR-029), webhook replay tolerance (FR-030).
- **File storage**: ✅ N/A for binaries (receipts are generated/rendered, not large blobs);
  if receipt PDFs are persisted they follow the R2/MinIO rule. Default: render-on-demand.
- **Caching/edge**: ✅ All payment/ledger responses are user-specific and **not** edge-cached;
  balances marked no-store. Stripe.js loaded from `js.stripe.com` (required, not bundled).
- **Testing discipline**: ✅ Test-first; backend persistence via PostgreSQL/Testcontainers
  with transaction-per-test isolation; xUnit Theories for data-varied cases (allocation
  orders, fee combos, webhook event types, opt-in matrices); frontend uses approved tools.
- **CI/CD and documentation**: ✅ Sonar, Codecov (≥95% changed-line coverage; repo also has a
  90% diff-coverage gate), env isolation, and Repowise marker regions updated for touched
  files (payments domain markers exist in `Program.cs` and `PaymentService.cs`).

**Observability clarification**: The constitution mandates Sentry; the codebase wires
**Sentry-on-OpenTelemetry** (Sentry consumes the OTel pipeline) plus an OTLP/Aspire path.
Both coexist — the Sentry mandate is satisfied; no deviation. Telemetry init is non-fatal.

**Gate result**: PASS (no unjustified violations). Two **legal** items are flagged in-spec as
*verify-with-counsel* (NC credit-card-surcharge legality; exact NC late-fee/interest caps) —
these are configuration/legal decisions, not architectural blockers; the model defaults to a
conservative flat convenience-fee posture and per-jurisdiction gating.

## Project Structure

### Documentation (this feature)

```text
specs/006-stripe-payments/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (updated for expanded spec)
├── data-model.md        # Phase 1 — entities/enums/relationships (updated)
├── quickstart.md        # Phase 1 — dev setup (updated)
├── contracts/
│   ├── payments-api.md   # REST contracts (updated: statements, jobs, idempotency)
│   └── webhook-events.md # Stripe event handling (updated: partial refund, dispute resolve, ACH return)
├── checklists/
│   └── requirements.md
├── design/              # Wireframe handoff bundle (source of truth for UI)
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/                         # .NET API
├── Domain/
│   ├── Entities/        # + PaymentTransaction, PaymentAuthorization, AlertConsent,
│   │                    #   WebhookEventInbox, OutboxMessage, HoaPaymentConfig, Receipt
│   │                    # ~ Owner, RecurringPayment, LedgerEntry, DraftEntry
│   └── Enums/           # + TransactionStatus, CardFunding, FeeType, CardScope, OutboxStatus,
│                        #   WebhookProcessingStatus; ~ LedgerEntryType, RecurringAmountType
├── Features/Payments/
│   ├── OneTime/         # intent + confirm endpoints (replaces simulated /one-time)
│   ├── Recurring/       # setup-intent, upsert/get/delete, mandate capture
│   ├── Alerts/          # alert-preferences get/update, consent capture, AlertService
│   ├── Webhooks/        # StripeWebhookEndpoint (durable intake)
│   ├── Jobs/            # run-drafts + reconcile internal endpoints (Cloud Scheduler)
│   ├── Statements/      # per-owner statement, unpaid-assessment statement
│   ├── Ledger/          # allocation + append-only balance service
│   └── Services/        # PaymentService, FeeCalculator, AllocationService, OutboxDispatcher,
│                        #   ReconciliationService, IStripeGateway, IAlertProvider
├── Infrastructure/
│   ├── Persistence/     # EF config + 1 migration
│   ├── Payments/        # Stripe gateway impl, Twilio/SendGrid providers
│   └── Observability/   # extend TelemetryScrubbingProcessor + payment metrics
└── Program.cs           # DI + middleware (extend)

neko-hoa/src/app/                              # Angular SPA
├── core/services/payments.service.ts          # ~ Stripe.js init + new endpoints
└── features/payments/
    ├── one-time/        # ~ Payment Element flow
    ├── recurring/       # ~ SetupIntent + mandate + alerts opt-in section
    └── statement/       # ~ statement / transactions view

HOAManagementCompany.Tests/Integration/Payments/   # xUnit + Testcontainers
└── (one-time, recurring, ledger/allocation, webhooks, reconciliation, alerts)
```

**Structure Decision**: Web-application layout (Option 2). The feature extends the existing
`Features/Payments/` feature-folder with sub-folders per concern, reuses the established
FastEndpoints + `PaymentService` + EF Core patterns, and adds Stripe/alert gateways behind
interfaces so integration tests mock the processor and providers.

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Features/Payments/PaymentService.cs` | `domain=payments` | Update: vaulted methods, fees, allocation, reversals |
| `HOAManagementCompany/Features/Payments/Webhooks/StripeWebhookEndpoint.cs` | `domain=payments-webhooks` | Durable intake + lifecycle handling |
| `HOAManagementCompany/Features/Payments/Jobs/*` | `domain=payments-jobs` | Draft batch + reconciliation sweep |
| `HOAManagementCompany/Program.cs` | `domain=bootstrap` | Stripe/Twilio/SendGrid DI + scheduler auth |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=example -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> Only deviations needing justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| External Cloud Scheduler + internal job endpoints (vs. in-process `BackgroundService`) | Cloud Run scales to zero; an in-process timer would not fire reliably and could miss draft days | A persistent always-on worker contradicts the scale-to-zero infra mandate and adds cost |
| New external dependencies (Stripe.net, Twilio, SendGrid) | Core to the feature (hosted PCI fields, SMS/email alerts) | No alternative; direct HTTP to these APIs is more error-prone than official SDKs |
| Durable webhook inbox + transactional outbox (vs. process-then-ack) | Prevents lost/duplicated events and alerts under crash/scale-to-zero (FR-032/FR-034) | Process-before-ack risks losing verified events when the instance is reaped mid-handling |
| Append-only ledger sequence + new entry types (vs. mutate `RunningBalance`) | Out-of-order ACH settlements + reversals corrupt the existing latest-row-minus-amount math | In-place balance updates are not safe with deferred/concurrent webhook-driven entries (SC-009) |

---
description: "Task list for Stripe Payments (One-Time & Recurring)"
---

# Tasks: Stripe Payments (One-Time & Recurring)

**Input**: Design documents from `/specs/006-stripe-payments/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the constitution's Quality Gates and Spec Kit Testing Constitution
mandate backend integration tests (PostgreSQL/Testcontainers, transaction-per-test) and
frontend tests for these flows; write tests first where the testing constitution applies.

**Organization**: Grouped by user story (US1 P1, US2 P2, US3 P3) for independent delivery.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: US1 / US2 / US3 (story phases only)

## Path Conventions

- Backend: `HOAManagementCompany/` (Domain, Features/Payments, Infrastructure)
- Backend tests: `HOAManagementCompany.Tests/Integration/Payments/`
- Frontend: `neko-hoa/src/app/` and `neko-hoa/cypress/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dependencies and configuration scaffolding

- [X] T001 Add NuGet packages `Stripe.net`, `Twilio`, `SendGrid` to `HOAManagementCompany/HOAManagementCompany.csproj`
- [X] T002 [P] Add `@stripe/stripe-js` and `ngx-stripe` to `neko-hoa/package.json` and lockfile
- [X] T003 [P] Add `Stripe`, `Twilio`, `SendGrid`, `Payments`, `Jobs` config sections to `HOAManagementCompany/appsettings.json` + `appsettings.Development.json` (no secrets) and strongly-typed options classes in `HOAManagementCompany/Features/Payments/PaymentOptions.cs`
- [X] T004 [P] Add `stripePublishableKey` to `neko-hoa/src/environments/environment.ts` and `environment.prod.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model, money/ledger engine, Stripe gateway, durable webhook + outbox + telemetry — shared by all stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain & persistence

- [X] T005 [P] Create enums `TransactionStatus`, `CardFunding`, `FeeType`, `CardScope`, `WebhookProcessingStatus`, `OutboxStatus`; extend `LedgerEntryType` (Refund, Reversal, Chargeback, ReturnedPaymentFee, Credit, Adjustment) in `HOAManagementCompany/Domain/Enums/`. **Reuse the existing `PaymentMethod` and `RecurringAmountType {Assessment,Balance,Fixed}` enums as-is** — they are string-persisted (`HasConversion<string>()`), so do NOT rename their type or values (a rename would orphan stored rows)
- [X] T006 [P] Create `PaymentTransaction` entity (gross/fee/total, cumulative refunded, settlement refs, idempotency key, card funding, return code) in `HOAManagementCompany/Domain/Entities/PaymentTransaction.cs`
- [X] T007 [P] Create `PaymentAuthorization` entity in `HOAManagementCompany/Domain/Entities/PaymentAuthorization.cs`
- [X] T008 [P] Create `AlertConsent` entity in `HOAManagementCompany/Domain/Entities/AlertConsent.cs`
- [X] T009 [P] Create `WebhookEventInbox` entity in `HOAManagementCompany/Domain/Entities/WebhookEventInbox.cs`
- [X] T010 [P] Create `OutboxMessage` entity in `HOAManagementCompany/Domain/Entities/OutboxMessage.cs`
- [X] T011 [P] Create `HoaPaymentConfig` entity in `HOAManagementCompany/Domain/Entities/HoaPaymentConfig.cs`
- [X] T012 [P] Create `Receipt` entity in `HOAManagementCompany/Domain/Entities/Receipt.cs`
- [X] T013 Modify `Owner` (StripeCustomerId, AlertSmsOptIn, AlertEmailOptIn, AlertPhone), `RecurringPayment` (VaultedPaymentMethodId, CurrentAuthorizationId; drop 7 masked fields), `LedgerEntry` (Sequence, CreatedAtUtc, TransactionId, FundCode), `DraftEntry` (TransactionId) in `HOAManagementCompany/Domain/Entities/`
- [X] T014 Add `DbSet`s and EF configuration (enum→string conversions, decimal precision, filtered unique indexes on `StripePaymentIntentId`/`IdempotencyKey`, unique `StripeEventId`, `(PropertyId,Sequence)` index, at-rest encryption for `AlertPhone`/IP fields) in `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs`
- [X] T015 Generate reversible EF migration `StripePayments` (no historical ledger-row drops) via `dotnet ef migrations add StripePayments` and confirm idempotent Cloud Run startup apply in `HOAManagementCompany/Program.cs`

### Money / ledger engine (test-first)

- [X] T016 [P] xUnit Theory test for `FeeCalculator` — percentage⇒credit-only, flat⇒all-cards, percentage+debit/all-cards rejected, ACH free, gross/fee split, rounding — in `HOAManagementCompany.Tests/Integration/Payments/FeeCalculatorTests.cs`
- [X] T017 [P] Testcontainers integration test for append-only ledger + allocation — deterministic `RunningBalance` recompute by `Sequence` under out-of-order/concurrent inserts (SC-009), statutory allocation order, overpayment→credit (negative balance), compensating reversals — in `HOAManagementCompany.Tests/Integration/Payments/LedgerAllocationTests.cs`
- [X] T018 Implement `FeeCalculator` (reads `HoaPaymentConfig`, uses Stripe `card.funding`) in `HOAManagementCompany/Features/Payments/Services/FeeCalculator.cs`
- [X] T019 Implement `LedgerService` (append-only `Sequence` under per-property lock, deterministic balance, compensating entries, overpayment credit) and `AllocationService` (category-priority, configurable order) in `HOAManagementCompany/Features/Payments/Ledger/`

### Stripe gateway, idempotency, webhook intake, outbox, telemetry

- [X] T020 [P] Define `IStripeGateway` (PaymentIntent, SetupIntent, Customer, off-session charge, balance-transaction/payout fetch, event construct) + `StripeGateway` impl in `HOAManagementCompany/Infrastructure/Payments/` and an in-memory fake in `HOAManagementCompany.Tests/Fixtures/FakeStripeGateway.cs`
- [X] T021 [P] Implement idempotency-key infrastructure (header binding, durable persistence on `PaymentTransaction.IdempotencyKey`, replay-returns-original, forward to Stripe `RequestOptions`) in `HOAManagementCompany/Features/Payments/Services/IdempotencyService.cs`
- [X] T022 Testcontainers integration test for durable webhook intake — signature verify, timestamp-tolerance replay rejection, inbox idempotency, retry→dead-letter — in `HOAManagementCompany.Tests/Integration/Payments/WebhookIntakeTests.cs` <!-- delivered as WebhookEndpointTests.cs + WebhookProcessorTests.cs -->

- [X] T023 Implement durable webhook intake core (`StripeWebhookEndpoint`: verify+timestamp tolerance → upsert `WebhookEventInbox` → ack 200 → dispatch; terminal-state guard) in `HOAManagementCompany/Features/Payments/Webhooks/StripeWebhookEndpoint.cs`
- [X] T024 [P] Implement transactional `OutboxMessage` write helper + `OutboxDispatcher` (no retry on provider rejection, records `alert.sent{success}`); dispatch **promptly in-process right after webhook ack** so alerts meet SC-006 ≤5 min, with the reconcile job as backstop (FR-034/Q1) in `HOAManagementCompany/Features/Payments/Alerts/OutboxDispatcher.cs` <!-- AlertService writes outbox rows in the webhook txn; OutboxDispatcher drains them -->`
- [X] T025 [P] Implement `ReconciliationService` scaffolding + `POST /payments/jobs/reconcile` endpoint with Cloud Scheduler auth (OIDC / `X-Scheduler-Secret`) in `HOAManagementCompany/Features/Payments/Jobs/`
- [X] T026 [P] Extend `TelemetryScrubbingProcessor` for Stripe/PII fields and add payment metrics (`payment.*`, `alert.sent`, `webhook.processed`) in `HOAManagementCompany/Infrastructure/Observability/` <!-- PaymentMetrics meter added + registered via AddMeter; scrubbing processor unchanged this split -->`
- [X] T027 [P] Seed default `HoaPaymentConfig` per HOA from `Payments:DefaultFee`/`Nsf` in `HOAManagementCompany/Seed/DatabaseSeeder.cs`
- [ ] T028 [P] Initialize Stripe.js (`loadStripe`) and expose `stripe$` in `neko-hoa/src/app/core/services/payments.service.ts`; register `provideNgxStripe()` in `neko-hoa/src/app/app.config.ts`
- [X] T029 Register all new gateways/services (Stripe, Twilio, SendGrid, FeeCalculator, Ledger/Allocation, Outbox, Reconciliation, Idempotency) in `HOAManagementCompany/Program.cs` DI <!-- alert split: PaymentMetrics, IAlertProvider x2, AlertService, OutboxDispatcher registered -->`

**Checkpoint**: Foundation ready — user stories can begin.

---

## Phase 3: User Story 1 - Pay an assessment one time, securely (Priority: P1) 🎯 MVP

**Goal**: A resident pays a one-time assessment via the embedded Stripe Payment Element (card synchronous, ACH pending→settled by webhook), with fee split, audit transaction, append-only ledger entry, durable receipt, and full refund/dispute/ACH-return lifecycle.

**Independent Test**: Drive the one-time wizard with a Stripe test card/bank; confirm the charge succeeds, a `PaymentTransaction` + `LedgerEntry` are written, raw PAN never reaches the backend, and a masked receipt is shown.

### Tests for User Story 1 ⚠️ (write first, ensure they FAIL)

- [X] T030 [P] [US1] Testcontainers Theory: one-time card success/decline → Succeeded/Failed txn, fee split, ledger entry on success, masked confirmation, receipt, in `HOAManagementCompany.Tests/Integration/Payments/OneTimePaymentTests.cs`
- [X] T031 [P] [US1] Testcontainers test: one-time ACH → Pending txn, ledger deferred until `payment_intent.succeeded` webhook writes it, in `HOAManagementCompany.Tests/Integration/Payments/OneTimeAchTests.cs`
- [X] T032 [P] [US1] Testcontainers Theory: webhook lifecycle — succeeded (settlement refs + deferred ledger + receipt), payment_failed, partial+full refund (cumulative, compensating ledger, fee retained), dispute created/closed won/lost, ACH return → Returned (+reversal +NSF) — in `HOAManagementCompany.Tests/Integration/Payments/WebhookLifecycleTests.cs`
- [X] T033 [P] [US1] Testcontainers test: idempotency key collapses double-submit to one charge/transaction, in `HOAManagementCompany.Tests/Integration/Payments/IdempotencyTests.cs`
- [ ] T034 [P] [US1] SC-001 test: confirm no raw card/bank number is accepted or stored (request + storage inspection), in `HOAManagementCompany.Tests/Integration/Payments/PciScopeTests.cs`
- [ ] T035 [P] [US1] Angular Testing Library component test for one-time payment component in `neko-hoa/src/app/features/payments/one-time/one-time.component.spec.ts`
- [ ] T036 [P] [US1] Jasmine unit test for `payments.service.ts` options/intent/confirm in `neko-hoa/src/app/core/services/payments.service.spec.ts`
- [ ] T037 [P] [US1] Cypress E2E for one-time payment in `neko-hoa/cypress/e2e/one-time-payment.cy.ts`
- [ ] T038 [P] [US1] Storybook story + visual case for the one-time payment summary in `neko-hoa/src/app/features/payments/one-time/one-time.stories.ts`

### Implementation for User Story 1

- [X] T039 [US1] Implement `GET /payments/options` (ledger balance, presets, fee config, credit balance) in `HOAManagementCompany/Features/Payments/OneTime/PaymentOptionsEndpoint.cs`
- [X] T040 [US1] Implement `POST /payments/intent` (create PaymentIntent incl. fee via `FeeCalculator`, idempotency, masked-method metadata) in `HOAManagementCompany/Features/Payments/OneTime/CreateIntentEndpoint.cs`
- [X] T041 [US1] Implement `POST /payments/one-time/confirm` (write `PaymentTransaction`; card→Succeeded+ledger+receipt, ACH→Pending) in `HOAManagementCompany/Features/Payments/OneTime/ConfirmPaymentEndpoint.cs`
- [X] T042 [US1] Implement webhook handlers `payment_intent.succeeded` (settlement refs, deferred ACH ledger, receipt) and `payment_intent.payment_failed` in `HOAManagementCompany/Features/Payments/Webhooks/Handlers/PaymentIntentHandlers.cs` <!-- consolidated into WebhookProcessor.cs -->
- [X] T043 [US1] Implement webhook handlers `charge.refunded`/`charge.refund.updated` (partial/cumulative compensating ledger, fee retention per FR-004d) in `HOAManagementCompany/Features/Payments/Webhooks/Handlers/RefundHandlers.cs` <!-- consolidated into WebhookProcessor.cs -->
- [X] T044 [US1] Implement webhook handlers `charge.dispute.created` and `charge.dispute.closed` (won→restore, lost→DisputeLost+NSF) in `HOAManagementCompany/Features/Payments/Webhooks/Handlers/DisputeHandlers.cs` <!-- consolidated into WebhookProcessor.cs -->
- [X] T045 [US1] Implement ACH-return-after-settlement handling (→Returned, reversal, NSF fee, alert hook) + confirm exact Stripe event in `HOAManagementCompany/Features/Payments/Webhooks/Handlers/AchReturnHandler.cs` <!-- consolidated into WebhookProcessor.HandleFailedAsync; alert-hook enqueue still pending (US3/T079) -->
- [X] T046 [US1] Wire reconciliation sweep to resolve ACH `Pending` past window (FR-033) in `HOAManagementCompany/Features/Payments/Jobs/ReconciliationService.cs`
- [X] T047 [US1] Implement `GET /payments/transactions` (pagination, status/isRecurring filters, masked method) replacing nothing — new in `HOAManagementCompany/Features/Payments/OneTime/TransactionsEndpoint.cs`
- [X] T048 [US1] Implement `GET /payments/receipts/{transactionId}` in `HOAManagementCompany/Features/Payments/OneTime/ReceiptEndpoint.cs`
- [X] T049 [US1] Remove the simulated one-time path (raw card/CVV handling) from `HOAManagementCompany/Features/Payments/PaymentService.cs` and `OneTimePaymentEndpoint.cs`/`PaymentModels.cs`
- [ ] T050 [P] [US1] Rebuild one-time component with Stripe Payment Element, presets, masked summary (Amount/Fee/Total), confirm flow in `neko-hoa/src/app/features/payments/one-time/one-time.component.ts`
- [ ] T051 [P] [US1] Add options/intent/confirm/transactions/receipt methods to `neko-hoa/src/app/core/services/payments.service.ts`
- [ ] T052 [US1] Add validation, consistent error shape, Serilog audit logging, and verify dev Swagger + PII-free Sentry/OTel spans for the one-time endpoints

**Checkpoint**: One-time payment is a complete, independently testable MVP slice.

---

## Phase 4: User Story 2 - Set up automatic recurring payment (Priority: P2)

**Goal**: A resident vaults a method via SetupIntent, accepts a recurring mandate (immutable authorization record), and the scheduled draft job charges the vaulted method off-session with the resolved amount + fee.

**Independent Test**: Complete auto-pay setup with a test method; verify a vaulted customer + PM reference (no raw numbers), mandate authorization persisted, toggle on/off, and a simulated draft produces a recurring transaction.

### Tests for User Story 2 ⚠️

- [X] T053 [P] [US2] Testcontainers test: setup-intent creates/reuses Stripe customer, vaults PM, stores only references, in `HOAManagementCompany.Tests/Integration/Payments/SetupIntentTests.cs`
- [X] T054 [P] [US2] Testcontainers test: recurring upsert persists `PaymentAuthorization` (text/version/IP/UA/terms), drops masked fields, requires mandate, in `HOAManagementCompany.Tests/Integration/Payments/RecurringSetupTests.cs`
- [X] T055 [P] [US2] Testcontainers Theory: run-drafts resolves amount (assessment/open-balance/fixed), applies fee, charges off-session, writes recurring txn + draft, idempotent per `{recurringId}:{period}`, in `HOAManagementCompany.Tests/Integration/Payments/RecurringDraftTests.cs`
- [ ] T056 [P] [US2] Testcontainers test: variable-amount advance notice enqueued before draft (FR-011c); disable→no drafts; **failed draft is NOT auto-retried within the cycle and waits for the next draft day (FR-011a)**, in `HOAManagementCompany.Tests/Integration/Payments/RecurringNoticeTests.cs`
- [ ] T057 [P] [US2] Angular Testing Library test for auto-pay component in `neko-hoa/src/app/features/payments/recurring/recurring.component.spec.ts`
- [ ] T058 [P] [US2] Cypress E2E for auto-pay setup in `neko-hoa/cypress/e2e/recurring-setup.cy.ts`
- [ ] T059 [P] [US2] Storybook story for the auto-pay status card + drafts table in `neko-hoa/src/app/features/payments/recurring/recurring.stories.ts`

### Implementation for User Story 2

- [X] T060 [US2] Implement `POST /payments/setup-intent` (create/reuse customer, SetupIntent) in `HOAManagementCompany/Features/Payments/Recurring/SetupIntentEndpoint.cs`
- [X] T061 [US2] Update `PUT /payments/recurring` (store vaulted PM, capture mandate→`PaymentAuthorization`, recurring fee) in `HOAManagementCompany/Features/Payments/Recurring/RecurringUpsertEndpoint.cs`
- [X] T062 [P] [US2] Update `GET /payments/recurring` (masked method, next draft date·amount incl. fee, mandate ref) in `HOAManagementCompany/Features/Payments/Recurring/RecurringGetEndpoint.cs`
- [X] T063 [P] [US2] Update `DELETE /payments/recurring` (disable + set `TerminatedAt` on authorization) in `HOAManagementCompany/Features/Payments/Recurring/RecurringDeleteEndpoint.cs`
- [X] T064 [US2] Implement `POST /payments/jobs/run-drafts` (due drafts, amount resolution, off-session charge, fee, draft entry, per-period idempotency, variable notice) in `HOAManagementCompany/Features/Payments/Jobs/RunDraftsEndpoint.cs`
- [ ] T065 [P] [US2] Update drafts query to surface status from linked `PaymentTransaction` and add `limit`/`offset` pagination (constitution §4) in `HOAManagementCompany/Features/Payments/DraftsEndpoint.cs`
- [ ] T066 [P] [US2] Rebuild auto-pay page (SetupIntent element, amount type, draft day, mandate checkbox, status card, drafts table) in `neko-hoa/src/app/features/payments/recurring/recurring.component.ts`
- [ ] T067 [P] [US2] Add setup-intent/recurring methods to `neko-hoa/src/app/core/services/payments.service.ts`
- [ ] T068 [US2] Add validation/error shape/audit logging and verify Swagger + PII-free telemetry for recurring + job endpoints

**Checkpoint**: One-time AND recurring both work independently.

---

## Phase 5: User Story 3 - Get alerted when an auto-pay charge fails (Priority: P3)

**Goal**: Residents opt in (default OFF, with TCPA consent record) to SMS/email; on a recurring failure or ACH return, an outbox-driven alert is sent only on opted-in channels.

**Independent Test**: Opt a resident into SMS+email, simulate a recurring failure, confirm an alert on each opted channel with masked content; non-opted resident gets none; one-time failure triggers no auto-pay alert.

### Tests for User Story 3 ⚠️

- [X] T069 [P] [US3] Testcontainers Theory: opt-in matrix (sms/email/both/neither) → alert only on opted channels; one-time failure → no alert, in `HOAManagementCompany.Tests/Integration/Payments/AlertOptInTests.cs`
- [X] T070 [P] [US3] Testcontainers test: provider send failure recorded (`alert.sent success=false` + errored span), not retried, webhook ack still 200 (FR-022a), in `HOAManagementCompany.Tests/Integration/Payments/AlertFailureTests.cs`
- [X] T071 [P] [US3] Testcontainers test: `AlertConsent` captured on opt-in; opt-out/STOP disables channel (FR-031), in `HOAManagementCompany.Tests/Integration/Payments/AlertConsentTests.cs`
- [ ] T072 [P] [US3] Angular Testing Library test for the payment-alerts opt-in section in `neko-hoa/src/app/features/payments/recurring/alerts/alerts.component.spec.ts`
- [ ] T073 [P] [US3] Cypress E2E for alert opt-in/opt-out in `neko-hoa/cypress/e2e/alert-preferences.cy.ts`

### Implementation for User Story 3

- [X] T074 [US3] Implement `GET /payments/alert-preferences` in `HOAManagementCompany/Features/Payments/Alerts/AlertPreferencesEndpoints.cs` <!-- GET+PUT colocated in AlertPreferencesEndpoints.cs -->
- [X] T075 [US3] Implement `PUT /payments/alert-preferences` (phone required for SMS, append `AlertConsent`) in `HOAManagementCompany/Features/Payments/Alerts/AlertPreferencesEndpoints.cs`
- [X] T076 [P] [US3] Implement `IAlertProvider` + `TwilioSmsProvider` (incl. STOP opt-out copy) in `HOAManagementCompany/Infrastructure/Payments/Alerts/TwilioSmsProvider.cs`
- [X] T077 [P] [US3] Implement `SendGridEmailProvider` in `HOAManagementCompany/Infrastructure/Payments/Alerts/SendGridEmailProvider.cs`
- [X] T078 [US3] Implement `AlertService` (channel selection, masked content, outbox-driven dispatch) in `HOAManagementCompany/Features/Payments/Alerts/AlertService.cs`
- [X] T079 [US3] Wire failure-alert enqueue on recurring `payment_failed` (FR-015) and ACH return (FR-014c) into the webhook handlers via outbox in `HOAManagementCompany/Features/Payments/Webhooks/WebhookProcessor.cs` <!-- consolidated WebhookProcessor, not Handlers/ -->`
- [ ] T080 [P] [US3] Add the "Payment alerts" opt-in section + service methods in `neko-hoa/src/app/features/payments/recurring/alerts/alerts.component.ts` and `payments.service.ts`

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Reporting, compliance, security, and gate verification across stories.

- [ ] T081 [P] Implement `GET /payments/statements` + `GET /payments/unpaid-assessments` (FR-039, NC § 47F-3-118) in `HOAManagementCompany/Features/Payments/Statements/` with Testcontainers tests
- [ ] T082 [P] Build the Angular statement/transactions view in `neko-hoa/src/app/features/payments/statement/statement.component.ts`
- [ ] T083 [P] Reconciliation + dead-letter hardening test (missed-webhook backfill, outbox flush, inbox retry) in `HOAManagementCompany.Tests/Integration/Payments/ReconciliationTests.cs`
- [ ] T084 [P] PII encryption-at-rest review (FR-029) + audit logging of financial-record access and fee/alert/schedule config changes
- [ ] T085 [P] Rate-limit review on intent/confirm/setup/jobs endpoints + processor fraud-tooling (Stripe Radar) note (FR-028)
- [ ] T086 [P] Document NC late-fee/interest caps config seed + surcharge-jurisdiction gating; confirm `SurchargingEnabled` defaults safe
- [ ] T087 [P] Document backups/PITR + RPO/RTO + Stripe-based reconstruction (FR-036) in `specs/006-stripe-payments/quickstart.md` ops section
- [ ] T088 Accessibility pass (WCAG 2.1 AA) for payment, auto-pay, alerts, and statement surfaces
- [ ] T089 Verify SC-008: confirm 0 deprecated masked card/bank columns remain post-migration; migration rollback/mitigation review
- [ ] T090 [P] Update Repowise marker regions for `PaymentService.cs`, `Program.cs`, webhook + jobs files
- [ ] T091 Verify Sonar PR scan, Codecov ≥95% changed-file coverage, and the 90% diff-coverage gate pass
- [ ] T092 Run `quickstart.md` end-to-end validation (Stripe CLI webhooks, draft + reconcile job curls)
- [ ] T093 [P] Add a Playwright browser test for the Stripe Payment Element interaction (iframe field entry outside the Cypress E2E suite, constitution §9) in `neko-hoa/tests/playwright/payment-element.spec.ts`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**.
- **User Stories (Phase 3–5)**: all depend on Foundational. US1 is the MVP; US2 and US3 build on the same foundation. US3 reuses US1's webhook handlers and US2's recurring failures, so it is most valuable after US2 but is independently testable via simulated events.
- **Polish (Phase 6)**: depends on the targeted user stories.

### User Story Dependencies

- **US1 (P1)**: after Foundational. No dependency on other stories.
- **US2 (P2)**: after Foundational. Independent of US1 (shares the ledger/gateway foundation).
- **US3 (P3)**: after Foundational. Functionally exercised by US2's recurring failures / US1's ACH returns, but testable standalone with simulated webhook events.

### Within Each User Story

- Tests first (must fail) → models/services → endpoints → frontend → integration/verify.
- Models before services; services before endpoints; backend before the Angular surface.

### Parallel Opportunities

- Setup: T002, T003, T004 in parallel.
- Foundational entities T005–T012 in parallel; then T013→T014→T015 sequential (same files/migration). Service tests T016/T017 parallel; gateway/idempotency/outbox/telemetry/frontend T020–T028 largely parallel.
- Within a story, all `[P]` test tasks run together; `[P]` frontend/service files run alongside backend endpoints in different files.
- With staff: US1, US2, US3 can proceed concurrently once Phase 2 is done.

---

## Parallel Example: User Story 1

```bash
# Tests for US1 together:
Task: "Testcontainers one-time card/decline tests (OneTimePaymentTests.cs)"
Task: "Testcontainers ACH pending/settlement test (OneTimeAchTests.cs)"
Task: "Webhook lifecycle Theory (WebhookLifecycleTests.cs)"
Task: "Cypress one-time E2E (one-time-payment.cy.ts)"

# Parallel frontend + service while backend endpoints land:
Task: "Rebuild one-time component (one-time.component.ts)"
Task: "Add payments.service.ts one-time methods"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2. Phase 2 Foundational (critical) → 3. Phase 3 US1 → 4. **STOP & validate** one-time end-to-end → 5. demo.

### Incremental Delivery

Foundation → US1 (MVP, demo) → US2 (auto-pay, demo) → US3 (alerts, demo) → Polish (statements, compliance, gates). Each story adds value without breaking prior ones.

### Notes

- `[P]` = different files, no incomplete dependencies.
- Backend integration tests use PostgreSQL/Testcontainers with transaction-per-test isolation; use xUnit Theories for the data-varied cases (fee combos, allocation orders, webhook event types, opt-in matrix).
- Stripe/Twilio/SendGrid are mocked via `IStripeGateway`/`IAlertProvider` — no real external calls in tests.
- Commit after each task or logical group; verify tests fail before implementing.

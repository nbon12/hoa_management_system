---
description: "PR Split 1 — US3 failure-alert backend (outbox + providers + wiring) and foundational closeout"
---

# Split 1 — Failure alerts (backend) + foundational closeout

**Story:** US3 — *Get alerted when an auto-pay charge fails* (Priority P3), backend only.
**Suggested branch:** `006-stripe-payments-alerts-backend`
**Depends on:** PR #16 (webhook handlers) and PR #19 (recurring) — both merged. No frontend.

## Goal

A resident who has opted in (default OFF, TCPA-compliant) receives an SMS and/or email when a recurring
charge fails (`payment_intent.payment_failed`, FR-015) or a settled ACH debit is returned (FR-014c).
Alerts are enqueued through a transactional **outbox** and dispatched promptly in-process right after
webhook ack (SC-006 ≤5 min), with the existing reconcile job as backstop. One-time failures send no alert.

This PR also closes the last foundational gaps: the outbox, the payment/alert/webhook **metrics**, and
the final DI wiring — and it satisfies the alert-hook tail of T045 that US1 left deferred.

## Tasks

| Task | Description | Primary file(s) |
|------|-------------|-----------------|
| T024 | Transactional `OutboxMessage` write helper + `OutboxDispatcher` (no retry on provider rejection; records `alert.sent{success}`); in-process dispatch right after webhook ack, reconcile job as backstop (FR-034/Q1) | `Features/Payments/Services/Outbox*.cs` |
| T076 | `IAlertProvider` + `TwilioSmsProvider` (incl. STOP opt-out copy) | `Infrastructure/Payments/TwilioSmsProvider.cs` |
| T077 | `SendGridEmailProvider` | `Infrastructure/Payments/SendGridEmailProvider.cs` |
| T078 | `AlertService` (channel selection, masked content, outbox-driven dispatch) | `Features/Payments/Alerts/AlertService.cs` |
| T074 | `GET /payments/alert-preferences` | `Features/Payments/Alerts/AlertPreferencesGetEndpoint.cs` |
| T075 | `PUT /payments/alert-preferences` (phone required for SMS, append `AlertConsent`) | `Features/Payments/Alerts/AlertPreferencesUpdateEndpoint.cs` |
| T079 | Wire failure-alert enqueue on recurring `payment_failed` (FR-015) and ACH return (FR-014c) into the webhook handlers via outbox — **also closes the T045 alert-hook tail** | `Features/Payments/Webhooks/WebhookProcessor.cs` |
| T026 | Add payment metrics (`payment.*`, `alert.sent`, `webhook.processed`) — the PII/Stripe **scrubbing** half already shipped; this PR adds the metric instruments | `Infrastructure/Observability/` |
| T029 | Final DI registration now that Twilio/SendGrid/Outbox/AlertService exist (Stripe, Fee, Ledger, Reconciliation, Idempotency already registered) | `Program.cs` |

## Tests (test-first)

| Task | Test |
|------|------|
| T069 | Theory: opt-in matrix (sms/email/both/neither) → alert only on opted channels; one-time failure → no alert. `AlertOptInTests.cs` |
| T070 | Provider send failure recorded (`alert.sent success=false` + errored span), **not retried**, webhook ack still 200 (FR-022a). `AlertFailureTests.cs` |
| T071 | `AlertConsent` captured on opt-in; opt-out/STOP disables channel (FR-031). `AlertConsentTests.cs` |

Drive the failure paths through `FakeStripeGateway` events and a fake `IAlertProvider` that can be
forced to reject. Assert outbox rows transition correctly and that a rejected send does not roll back
the webhook ack.

## Definition of done

- Opted-in resident gets alerts on a forced recurring failure / ACH return; non-opted gets none.
- Provider rejection is recorded, not retried, and never fails the webhook ack.
- New `payment.*` / `alert.sent` / `webhook.processed` metrics emit and carry no PII.
- All new services registered in DI; app boots; ≥90% diff coverage; CI green.

## Out of scope

Frontend opt-in UI (T080, T072, T073 → Split 4). Statements/reporting (Split 2).

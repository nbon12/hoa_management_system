# Stripe Webhook Event Contracts

**Feature**: 006-stripe-payments | **Date**: 2026-06-06

This document describes how each Stripe event type is handled by `POST /payments/webhooks/stripe`.

---

## Event Handling Matrix

| Stripe Event Type | HOA Action | Alert Triggered? |
|------------------|-----------|-----------------|
| `payment_intent.succeeded` | → `Succeeded`; if was `Pending` (ACH), write deferred `LedgerEntry`; capture settlement refs (balance txn, processor fee, payout) (FR-037); issue receipt (FR-007f) | No |
| `payment_intent.payment_failed` | → `Failed`; store `FailureCode`/`FailureMessage` | Yes, if `IsRecurring` AND opted in |
| `charge.refunded` / `charge.refund.updated` | Set `CumulativeRefundedAmount` from `charge.amount_refunded`; write compensating `Refund` ledger entry for the delta; status `PartiallyRefunded` (< total) or `Refunded` (== total) (FR-014a/b) | No |
| `charge.dispute.created` | → `Disputed`; write compensating `Chargeback` reversal (FR-014a) | No |
| `charge.dispute.closed` | won → re-reverse, back to `Succeeded`; lost → `DisputeLost` (reversal stands) + optional NSF fee (FR-014d/e) | No |
| ACH return after settlement *(see §ACH-return)* | → `Returned` (store `ReturnCode`); compensating `Reversal` entry; optional NSF fee (FR-014c/e) | Yes, if `IsRecurring` AND opted in |
| *(all other types)* | Log at `Information`: `"Unhandled Stripe event {type} {id}"` | No |

> All money movements above use **compensating, append-only** ledger entries (FR-007d/e) — the
> original ledger row is never mutated; the running balance recomputes by `Sequence`.

---

## Durable Intake & Idempotency Contract  *(updated — FR-032/FR-030/FR-017)*

Processing order on every webhook POST:

1. **Verify signature** via `EventUtility.ConstructEvent` with timestamp tolerance (rejects
   replayed/stale events, FR-030). On failure → `400`, **no data mutation**.
2. **Durable capture**: upsert `WebhookEventInbox` by `StripeEventId` (unique). If it already
   exists → ack `200` immediately (duplicate). Otherwise insert with status `Received` and the
   PII-scrubbed payload. **Ack `200` after capture** — before heavy processing — so no verified
   event is lost if the instance is reaped (Cloud Run scale-to-zero).
3. **Process** (after ack, or in the same handler then mark): route by `event.Type`; apply the
   **terminal-state guard** — skip status transitions on transactions already terminal
   (`Succeeded`/`Failed`/`Refunded`/`DisputeLost`/`Returned`) except the legal follow-ons
   (partial→full refund, dispute open→closed). Mark inbox `Processed`.
4. **On processing failure**: increment `Attempts`, leave `Received` for retry by the reconcile
   job; after the threshold → `DeadLettered` (FR-032). Alerts/receipts are written as
   `OutboxMessage` rows in the same DB transaction (FR-034).

This makes duplicate, out-of-order, missed, and crash-interrupted deliveries all safe.

---

## Event Detail: payment_intent.succeeded

**Stripe object**: `PaymentIntent`

**Backend steps**:
1. Resolve `PaymentTransaction` by `StripePaymentIntentId`.
2. If no matching transaction: log warning `"Webhook payment_intent.succeeded for unknown PaymentIntent {id}"`; persist event ID; return `200`.
3. If found and status is not terminal: set `Status = Succeeded`, `UpdatedAt = now`.
4. If `IsRecurring = false` AND previous status was `Pending` (ACH one-time): write `LedgerEntry` with `PaymentAmount = Transaction.Amount`, `RunningBalance = previous + Payment` (debits reduce balance).
5. If `IsRecurring = true` AND previous status was `Pending` (ACH recurring): write `LedgerEntry` same way.
6. Mark the `WebhookEventInbox` row `Processed`.

**OTel**: Emit `payment.succeeded` counter with tags `{ type: "card"|"ach", is_recurring: bool }`. Span name: `"webhook.payment_intent.succeeded"`.

---

## Event Detail: payment_intent.payment_failed

**Stripe object**: `PaymentIntent` with `last_payment_error`

**Backend steps**:
1. Resolve `PaymentTransaction` by `StripePaymentIntentId`.
2. If no matching transaction: log warning; persist event ID; return `200`.
3. If found and not terminal: set `Status = Failed`, `FailureCode = last_payment_error.code`, `FailureMessage = last_payment_error.message` (PII-scrubbed — strip cardholder name if present), `UpdatedAt = now`.
4. If `IsRecurring = true`: invoke `AlertService.SendRecurringFailureAlertAsync(ownerId, transactionId)`.
   - `AlertService` checks `Owner.AlertSmsOptIn` and `Owner.AlertEmailOptIn`.
   - Sends on opted-in channels only.
   - If send fails: record `alert.sent` metric `success=false`, mark span errored; do not rethrow.
5. Mark the `WebhookEventInbox` row `Processed`.

**OTel**: Emit `payment.failed` counter with tags `{ type, is_recurring }`. If recurring, emit `alert.sent` counter with `{ channel: "sms"|"email", success: bool }`. Span name: `"webhook.payment_intent.payment_failed"`.

---

## Event Detail: charge.refunded / charge.refund.updated  *(partial-refund aware)*

**Stripe object**: `Charge` (use `amount_refunded` as the cumulative source of truth)

**Backend steps**:
1. Resolve `PaymentTransaction` by `StripeChargeId`. If none: log warning; mark inbox; `200`.
2. Compute delta = `amount_refunded` − `CumulativeRefundedAmount`. If delta ≤ 0 (duplicate), skip.
3. Set `CumulativeRefundedAmount = amount_refunded`; status `Refunded` if it equals `Total`,
   else `PartiallyRefunded`.
4. Write a compensating `Refund` `LedgerEntry` for the **delta** (FR-014a/b). The convenience
   fee is retained unless this is a full HOA/processing-error refund (FR-004d).
5. Mark inbox `Processed`.

---

## Event Detail: charge.dispute.created

**Stripe object**: `Dispute` (resolve transaction by `dispute.charge`)

**Steps**: set `Status = Disputed`; write a compensating `Chargeback` reversal `LedgerEntry`
restoring the balance (funds held by Stripe) (FR-014a). Mark inbox `Processed`.

---

## Event Detail: charge.dispute.closed  *(resolution — FR-014d)*

**Steps**: read `dispute.status`.
- **won**: re-reverse — write a counter-entry restoring the charge; set `Status = Succeeded`.
- **lost**: set `Status = DisputeLost` (the earlier reversal stands); if `NsfFeeEnabled`, assess
  the returned-payment fee as a `ReturnedPaymentFee` charge (FR-014e).

---

## Event Detail: ACH return after settlement  *(FR-014c)*

A previously `Succeeded` `us_bank_account` charge that later returns (R01 insufficient funds,
R02 closed account, …). **Confirm the exact Stripe event during implementation** (`charge.failed`
on the settled charge vs `payment_intent.payment_failed` vs `charge.refunded` with a return
reason); the reconciliation sweep is the backstop if the event is missed.

**Steps**: set `Status = Returned`, store `ReturnCode`; write a compensating `Reversal`
`LedgerEntry`; if `IsRecurring` AND opted in → enqueue failure alert; if `NsfFeeEnabled` →
assess `ReturnedPaymentFee` charge (FR-014e). Mark inbox `Processed`.

---

## Failure Alert Content

### SMS (Twilio)

```
NekoHOA: Your auto-pay of $[total] on [date] failed. Update your payment method at [link]. Reply STOP to opt out.
```

- `[total]` — `PaymentTransaction.Total` formatted as currency
- `[date]` — `PaymentTransaction.CreatedAt` formatted as `MMM d, yyyy`
- `[link]` — deep link to auto-pay page (configured via `Payments:AutoPayPageUrl`)
- No PII (no card details, no names, no email)

### Email (SendGrid)

**Subject**: `NekoHOA: Auto-pay charge failed`

**Body summary**:
- Failed amount and date
- Brief reason (generic: "Your payment method was declined")
- Link to update payment method at the auto-pay page
- No card/bank details in body

---

## OTel Telemetry Summary

| Metric | Tags | When |
|--------|------|------|
| `payment.succeeded` | `type`, `is_recurring` | `payment_intent.succeeded` processed |
| `payment.failed` | `type`, `is_recurring` | `payment_intent.payment_failed` processed |
| `payment.refunded` | — | `charge.refunded` processed |
| `payment.disputed` | — | `charge.dispute.created` processed |
| `alert.sent` | `channel` (`sms`/`email`), `success` (`true`/`false`) | After each alert send attempt |
| `webhook.processed` | `event_type`, `outcome` (`handled`/`skipped`/`unknown_transaction`) | Every webhook invocation |

All spans must be free of PII. Payment amounts are permitted.

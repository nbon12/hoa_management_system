# Payments API Contracts

**Feature**: 006-stripe-payments | **Date**: 2026-06-06
**Base path**: Paths below are shown relative to `/payments/`; the deployed prefix is
`/api/v1` (FastEndpoints `RoutePrefix`), so the real path is e.g. `/api/v1/payments/intent`.
**Auth**: All endpoints require a valid JWT (`Authorization: Bearer <token>`) and a `propertyId` claim — **except** the webhook endpoint (Stripe signature) and the `/payments/jobs/*` endpoints (Cloud Scheduler OIDC / shared secret).
**Error shape**: `{ "code": "string", "message": "string" }` (existing convention)
**Timestamps**: UTC, ISO 8601
**Idempotency**: `POST /payments/intent`, `/payments/one-time/confirm`, and `/payments/setup-intent` accept an `Idempotency-Key` header (FR-007a). A repeated key returns the original result and is forwarded to Stripe; keys are persisted durably (survive restarts, FR-035).
**Fee fields**: money responses split `baseAmount`/`gross` and `fee` as distinct fields (FR-004b); never a single combined total only.

---

## GET /payments/options

Returns the data needed to populate the one-time payment amount presets.

**Auth**: Required  
**Caching**: Must not be cached (balance is live)

**Response 200**:
```json
{
  "currentBalance": 125.00,
  "nextAssessment": 35.00,
  "nextAssessmentDueDate": "2026-07-01",
  "creditBalance": 0.00,
  "fee": { "cardFeeType": "Percentage", "cardFeeValue": 0.03, "cardScope": "CreditOnly", "achFee": 0.00 }
}
```

| Field | Type | Notes |
|-------|------|-------|
| `currentBalance` | decimal | Recomputed from append-only ledger (ordered by `Sequence`) |
| `nextAssessment` | decimal | `Property.MonthlyAssessment` |
| `nextAssessmentDueDate` | string (date) | First day of next month |
| `creditBalance` | decimal | On-account credit from any overpayment (FR-007c), 0 if none |
| `fee` | object | Per-HOA fee model from `HoaPaymentConfig` (FR-004b): `cardFeeType` (Flat\|Percentage), `cardFeeValue`, `cardScope` (AllCards\|CreditOnly), `achFee`. Final fee still computed server-side at intent time (depends on card funding). |

---

## POST /payments/intent

Creates a Stripe `PaymentIntent` for a one-time payment. Returns the `clientSecret` that the frontend uses to mount the Stripe Payment Element and confirm the payment.

**Auth**: Required  
**Rate limit**: `payments` policy (existing)

**Request**:
```json
{
  "amountCents": 12895,
  "paymentMethodType": "card"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `amountCents` | int | Yes | Base amount in cents (before surcharge); must be > 0 |
| `paymentMethodType` | string | Yes | `"card"` or `"ach"` |

**Validation**:
- `amountCents` > 0
- `paymentMethodType` in `["card", "ach"]`

**Response 200**:
```json
{
  "clientSecret": "pi_abc_secret_xyz",
  "paymentIntentId": "pi_abc",
  "baseAmount": 128.95,
  "fee": 3.87,
  "total": 132.82,
  "currency": "usd"
}
```

**Errors**:
- `400` — validation failure
- `502` — Stripe API unreachable

---

## POST /payments/one-time/confirm

Called by the frontend after `stripe.confirmPayment()` resolves (redirect or return URL callback). Backend retrieves the `PaymentIntent` from Stripe, writes the `PaymentTransaction` and (for card success) the `LedgerEntry`, and returns a masked confirmation.

**Auth**: Required

**Request**:
```json
{
  "paymentIntentId": "pi_abc"
}
```

**Response 200** (card — synchronous success):
```json
{
  "transactionId": "uuid",
  "status": "Succeeded",
  "maskedMethod": "Visa •• 4242",
  "baseAmount": 128.95,
  "fee": 3.87,
  "total": 132.82,
  "confirmation": "CONF-A1B2C3D4",
  "processedAt": "2026-06-06T14:23:00Z"
}
```

**Response 200** (ACH — pending):
```json
{
  "transactionId": "uuid",
  "status": "Pending",
  "maskedMethod": "Chase •• 6789",
  "baseAmount": 35.00,
  "fee": 0.00,
  "total": 35.00,
  "confirmation": "CONF-E5F6G7H8",
  "processedAt": "2026-06-06T14:25:00Z"
}
```

**Errors**:
- `400` — `paymentIntentId` missing or does not belong to this property
- `422` — PaymentIntent in Failed status (returns failure details)
- `502` — Stripe API unreachable

---

## POST /payments/setup-intent

Creates a Stripe `SetupIntent` to vault a payment method for recurring auto-pay. Creates (or reuses) a Stripe Customer for the resident.

**Auth**: Required

**Request**: empty body (property + owner resolved from JWT claims)

**Response 200**:
```json
{
  "clientSecret": "seti_abc_secret_xyz",
  "setupIntentId": "seti_abc",
  "stripeCustomerId": "cus_xyz"
}
```

**Errors**:
- `502` — Stripe API unreachable

---

## GET /payments/recurring

Returns the current auto-pay configuration for the resident's property.

**Auth**: Required

**Response 200** (auto-pay active):
```json
{
  "id": "uuid",
  "isEnabled": true,
  "amountType": "Assessment",
  "fixedAmount": null,
  "paymentMethodType": "Ach",
  "draftDay": 15,
  "maskedMethod": "Chase •• 6789 · ACH · no fee",
  "nextDraftDate": "2026-07-15",
  "nextDraftAmount": 35.00,
  "mandateAcceptedAt": "2026-06-06T14:30:00Z",
  "processingFee": 0.00,
  "updatedAt": "2026-06-06T14:30:00Z"
}
```

**Response 200** (no auto-pay):
```json
null
```

---

## PUT /payments/recurring

Creates or updates auto-pay configuration. Stores vaulted payment method reference. Never stores raw or masked card/bank data.

**Auth**: Required  
**Rate limit**: `payments` policy

**Request**:
```json
{
  "paymentMethodId": "pm_stripe_id",
  "draftDay": 15,
  "amountType": "Assessment",
  "fixedAmount": null,
  "mandateAccepted": true
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `paymentMethodId` | string | Yes | Stripe `pm_...` returned from SetupIntent confirmation |
| `draftDay` | int | Yes | 1–28 |
| `amountType` | string | Yes | `Assessment`, `Balance`, `Fixed` (UI labels differ; enum values stay these) |
| `fixedAmount` | decimal | Conditional | Required when `amountType = Fixed`; > 0 |
| `mandateAccepted` | bool | Yes | Must be `true`; 400 if false |

**Response 200**: Same shape as `GET /payments/recurring`

**Errors**:
- `400` — validation failure or mandate not accepted
- `502` — Stripe API unreachable (customer creation/PM attachment)

---

## DELETE /payments/recurring

Disables auto-pay. Sets `RecurringPayment.Status = "inactive"`. No further drafts occur.

**Auth**: Required

**Response 204**: No content

---

## GET /payments/transactions

Returns the paginated transaction audit trail for the resident's property.

**Auth**: Required  
**Pagination**: `limit` (default 20, max 100) and `offset` (default 0)

**Query parameters**:

| Param | Type | Notes |
|-------|------|-------|
| `limit` | int | Default 20, max 100 |
| `offset` | int | Default 0 |
| `status` | string | Optional filter: `Pending`, `Succeeded`, `Failed`, `PartiallyRefunded`, `Refunded`, `Disputed`, `DisputeLost`, `Returned` |
| `isRecurring` | bool | Optional filter |

**Response 200**:
```json
{
  "items": [
    {
      "id": "uuid",
      "paymentIntentId": "pi_abc",
      "grossAmount": 128.95,
      "fee": 3.87,
      "total": 132.82,
      "cumulativeRefunded": 0.00,
      "currency": "usd",
      "status": "Succeeded",
      "paymentMethodType": "Card",
      "cardFunding": "credit",
      "maskedMethod": "Visa •• 4242",
      "isRecurring": false,
      "failureCode": null,
      "failureMessage": null,
      "returnCode": null,
      "createdAt": "2026-06-06T14:23:00Z",
      "updatedAt": "2026-06-06T14:23:00Z"
    }
  ],
  "total": 1,
  "limit": 20,
  "offset": 0
}
```

**Note**: `maskedMethod` is derived from Stripe `PaymentIntent` metadata set at intent creation time (brand + last4 for card; bank name + last4 for ACH). Raw payment method details are never stored.

---

## GET /payments/alert-preferences

Returns the resident's payment alert opt-in preferences.

**Auth**: Required

**Response 200**:
```json
{
  "smsOptIn": false,
  "emailOptIn": false,
  "alertPhone": null,
  "alertEmail": "resident@example.com"
}
```

---

## PUT /payments/alert-preferences

Updates alert opt-in preferences.

**Auth**: Required

**Request**:
```json
{
  "smsOptIn": true,
  "emailOptIn": true,
  "alertPhone": "+15105551234",
  "alertEmail": "resident@example.com"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `smsOptIn` | bool | Yes | |
| `emailOptIn` | bool | Yes | |
| `alertPhone` | string | Conditional | Required when `smsOptIn = true`; E.164 format |
| `alertEmail` | string | Conditional | Required when `emailOptIn = true` |

**Validation**:
- `alertPhone` is required when `smsOptIn = true`
- `alertEmail` is required when `emailOptIn = true`; must be valid email

**Response 200**: Same shape as `GET /payments/alert-preferences`

---

## POST /payments/webhooks/stripe

Stripe webhook endpoint. Unauthenticated by JWT — authenticated by Stripe signature verification.

**Auth**: Stripe `Stripe-Signature` header (HMAC-SHA256 with `Payments:WebhookSigningSecret`)  
**Content-Type**: `application/json` (raw body required for signature verification)  
**Rate limit**: None (Stripe IPs only — consider Cloudflare WAF allowlist)

**Processing**:
1. Read raw body bytes.
2. Verify `Stripe-Signature` via `EventUtility.ConstructEvent` with timestamp tolerance (FR-030); return `400` on failure **without mutating data**.
3. Durably capture into `WebhookEventInbox` by `StripeEventId` (unique); if it already exists, return `200` immediately (duplicate). Ack `200` after capture, before heavy processing (FR-032).
4. Route by `event.Type`:
   - `payment_intent.succeeded` → transition `PaymentTransaction` to Succeeded; write `LedgerEntry` if was Pending (ACH settlement)
   - `payment_intent.payment_failed` → transition to Failed; store `FailureCode` + `FailureMessage`; trigger alert if recurring + opted in
   - `charge.refunded` → transition to Refunded
   - `charge.dispute.created` → transition to Disputed
   - *(all others)* → log at `Information`; do not mutate
5. Mark the `WebhookEventInbox` row `Processed`; on processing failure, leave it for retry / dead-letter (FR-032).
6. Alerts/receipts are enqueued to `OutboxMessage` in the same transaction and dispatched promptly (FR-034).

**Response**: `200 OK` (always, unless signature verification fails → `400`). Body: `{}` or empty.

**SLA**: Must acknowledge within Stripe's 30-second timeout. Heavy processing must not block acknowledgement.

---

## GET /payments/statements

Per-owner account statement (charges, payments, running balance, credits) for the resident's
property — supports NC owner record-inspection rights (FR-039, § 47F-3-118).

**Auth**: Required · **Pagination**: `limit`/`offset` · **Caching**: no-store

**Query**: `startDate`, `endDate` (optional, ISO date).

**Response 200**:
```json
{
  "openingBalance": 90.00,
  "closingBalance": 35.00,
  "creditBalance": 0.00,
  "lines": [
    { "sequence": 1012, "date": "2026-06-01", "type": "RegularAssessment", "charge": 35.00, "payment": 0.00, "balance": 125.00, "transactionId": null },
    { "sequence": 1013, "date": "2026-06-06", "type": "Payment", "charge": 0.00, "payment": 90.00, "balance": 35.00, "transactionId": "uuid" }
  ],
  "total": 2, "limit": 50, "offset": 0
}
```

---

## GET /payments/unpaid-assessments

Statement of unpaid assessments / payoff figure (FR-039). Returns outstanding charges grouped
by category in allocation order.

**Auth**: Required

**Response 200**:
```json
{
  "totalDue": 35.00,
  "asOf": "2026-06-06T14:23:00Z",
  "buckets": [
    { "category": "RegularAssessment", "amount": 35.00 },
    { "category": "LateFee", "amount": 0.00 }
  ]
}
```

---

## GET /payments/receipts/{transactionId}

Retrieve a durable payment receipt (FR-007f). Issued at card success / ACH settlement.

**Auth**: Required (must own the transaction's property)

**Response 200**:
```json
{
  "transactionId": "uuid",
  "confirmation": "CONF-A1B2C3D4",
  "maskedMethod": "Visa •• 4242",
  "grossAmount": 128.95, "fee": 3.87, "total": 132.82,
  "issuedAt": "2026-06-06T14:23:05Z"
}
```
**Errors**: `404` — no receipt yet (e.g. ACH not settled).

---

## POST /payments/jobs/run-drafts  *(internal — Cloud Scheduler)*

Processes auto-pay drafts due today (FR-010). Charges vaulted methods off-session, writes
recurring `PaymentTransaction`s + `DraftEntry`s, sends variable-amount notices (FR-011c), and
enqueues outbox messages.

**Auth**: Cloud Scheduler OIDC token (or shared-secret header); **not** session-auth. Source-
restricted via Cloudflare. **Idempotency**: per draft `draft:{recurringId}:{period}` (FR-011d)
— safe to re-run.

**Response 200**: `{ "processed": 12, "succeeded": 11, "failed": 1, "skippedDuplicate": 0 }`

---

## POST /payments/jobs/reconcile  *(internal — Cloud Scheduler)*

Reconciliation sweep (FR-033) + outbox flush (FR-034) + webhook-inbox retry (FR-032). Polls
Stripe for charge/event status and resolves any transaction left non-terminal past its window
(e.g. ACH `Pending`), catching missed webhooks.

**Auth**: same as `run-drafts`.

**Response 200**: `{ "reconciled": 3, "outboxSent": 5, "inboxRetried": 1, "deadLettered": 0 }`

---

## Breaking Change Notes

- `POST /payments/one-time` (old endpoint) is replaced by the two-step `POST /payments/intent` + `POST /payments/one-time/confirm`. Old frontend code must be updated.
- `PUT /payments/recurring` request body changes: removes raw card/bank fields, adds `paymentMethodId` and `mandateAccepted`. Existing frontend code for recurring setup must be replaced with the SetupIntent flow.
- `GET /payments/recurring` response changes: removes masked card/bank fields, adds `maskedMethod`, `mandateAcceptedAt`, `nextDraftDate`, `nextDraftAmount`.

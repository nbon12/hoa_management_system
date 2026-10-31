# Payments API Contracts

**Feature**: 006-stripe-payments | **Date**: 2026-06-06
**Base path**: Paths below are shown relative to `/payments/`; the deployed prefix is
`/api/v1` (FastEndpoints `RoutePrefix`), so the real path is e.g. `/api/v1/payments/intent`.
**Auth**: All endpoints require a valid JWT (`Authorization: Bearer <token>`) and a `propertyId` claim — **except** the webhook endpoint (Stripe signature) and the `/payments/jobs/*` endpoints (`X-Scheduler-Secret` header). *Superseded in part by specs/030-company-administration (FR-024g, FR-012a):* the `propertyId` claim only selects the home. Every resident endpoint re-checks the caller's resident link and the community's status on each request: no link → `403 PROPERTY_ACCESS_DENIED`; Onboarding community → `409 COMMUNITY_NOT_ACTIVE`; Archived community → `409 COMMUNITY_ARCHIVED`, except reads of the ledger, statements, receipts and transactions.
**Error shape**: `{ "code": "string", "message": "string" }` (existing convention). Request-validation failures return `422` with `{ "code": "VALIDATION_ERROR", "message": "…", "errors": [{ "field", "message" }] }`. An unhandled Stripe API error returns `500` (global exception handler).
**Timestamps**: UTC, ISO 8601
**Idempotency**: `POST /payments/intent` and `/payments/one-time/confirm` accept an `Idempotency-Key` header (FR-007a); `POST /payments/recurring/setup-intent` does not. A repeated key returns the original result and is forwarded to Stripe; keys are persisted durably (survive restarts, FR-035).
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
  "cardFeeType": "Percentage",
  "cardFeeValue": 0.03,
  "cardScope": "CreditOnly",
  "surchargingEnabled": true,
  "achFeeValue": 0.00
}
```

| Field | Type | Notes |
|-------|------|-------|
| `currentBalance` | decimal | Recomputed from append-only ledger (ordered by `Sequence`) |
| `nextAssessment` | decimal | `Property.MonthlyAssessment` |
| `nextAssessmentDueDate` | string (date) or null | Next occurrence of `Property.AssessmentDueDay` (null when unset) |
| `creditBalance` | decimal | On-account credit from any overpayment (FR-007c), 0 if none |
| `cardFeeType`, `cardFeeValue`, `cardScope`, `surchargingEnabled`, `achFeeValue` | flat fields | Per-HOA fee model from `HoaPaymentConfig` (FR-004b): `cardFeeType` (Flat\|Percentage), `cardScope` (AllCards\|CreditOnly). Final fee still computed server-side at intent time (depends on card funding). |

---

## POST /payments/intent

Creates a Stripe `PaymentIntent` for a one-time payment. Returns the `clientSecret` that the frontend uses to mount the Stripe Payment Element and confirm the payment.

**Auth**: Required  
**Rate limit**: `payments` policy (existing)

**Request**:
```json
{
  "amount": 128.95,
  "method": "card"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `amount` | decimal | Yes | Base amount in dollars (before fee); must be > 0 |
| `method` | string | Yes | `"card"` or `"ach"` (lower-case) |

**Validation**:
- `amount` > 0
- `method` in `["card", "ach"]`

**Response 200**:
```json
{
  "paymentIntentId": "pi_abc",
  "clientSecret": "pi_abc_secret_xyz",
  "amount": 128.95,
  "fee": 3.87,
  "total": 132.82
}
```

**Errors**:
- `422` — validation failure
- `500` — Stripe API error

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
  "grossAmount": 128.95,
  "feeAmount": 3.87,
  "total": 132.82,
  "maskedMethod": "Visa •• 4242",
  "confirmationNumber": "CONF-A1B2C3D4E5",
  "receiptId": "uuid"
}
```

**Response 200** (ACH — pending):
```json
{
  "transactionId": "uuid",
  "status": "Pending",
  "grossAmount": 35.00,
  "feeAmount": 0.00,
  "total": 35.00,
  "maskedMethod": "Chase •• 6789",
  "confirmationNumber": null,
  "receiptId": null
}
```

**Errors**:
- `422` — `paymentIntentId` missing (validation)
- `403` — the PaymentIntent's `propertyId` metadata is not the caller's property
- `422` — PaymentIntent in Failed status (returns failure details)
- `500` — Stripe API error

---

## POST /payments/recurring/setup-intent

Creates a Stripe `SetupIntent` to vault a payment method for recurring auto-pay. Creates (or reuses) a Stripe Customer for the resident.

**Auth**: Required

**Request**: empty body (property + owner resolved from JWT claims)

**Response 200**:
```json
{
  "setupIntentId": "seti_abc",
  "clientSecret": "seti_abc_secret_xyz",
  "publishableKey": "pk_test_..."
}
```

**Rate limit**: `payments` policy

**Errors**:
- `404` — no owner record for the property
- `500` — Stripe API error

---

## GET /payments/recurring

Returns the current auto-pay configuration for the resident's property.

**Auth**: Required

**Response 200** (auto-pay active):
```json
{
  "id": "uuid",
  "amountType": "Assessment",
  "fixedAmount": null,
  "method": "Ach",
  "draftDay": 15,
  "status": "active",
  "maskedMethod": "Bank ····6789",
  "nextDraftDate": "2026-07-15",
  "nextDraftAmount": 35.00,
  "mandateAcceptedAt": "2026-06-06T14:30:00Z",
  "processingFee": 0.00
}
```

**Response 204** (no active auto-pay): no body.

---

## PUT /payments/recurring

Creates or updates auto-pay configuration. Stores the vaulted payment method reference plus Stripe-provided display detail (card brand, last 4, card funding). Never stores raw or self-masked card/bank numbers (amended 2026-10-31 to match PR #19).

**Auth**: Required  
**Rate limit**: `payments` policy

**Request**:
```json
{
  "amountType": "assessment",
  "fixedAmount": null,
  "draftDay": 15,
  "setupIntentId": "seti_abc",
  "mandateAccepted": true,
  "mandateText": "I authorize recurring payments of …",
  "mandateVersion": "1.0"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `setupIntentId` | string | Yes | Stripe `seti_...` the browser confirmed; the backend reads the vaulted `pm_...` from it |
| `draftDay` | int | Yes | 1–28 |
| `amountType` | string | Yes | `assessment`, `balance` or `fixed` (lower-case; stored and returned as `Assessment`/`Balance`/`Fixed`) |
| `fixedAmount` | decimal | Conditional | Required when `amountType = Fixed`; > 0 |
| `mandateAccepted` | bool | Yes | Must be `true`; 422 if false |
| `mandateText` | string | No | Exact mandate text shown; a server default is used when absent |
| `mandateVersion` | string | No | Defaults to `1.0` |

**Response 200**: Same shape as `GET /payments/recurring`

**Errors**:
- `422` — validation failure or mandate not accepted
- `500` — Stripe API error (customer creation / SetupIntent retrieval)

---

## DELETE /payments/recurring

Disables auto-pay. Sets `RecurringPayment.Status = "inactive"` and `TerminatedAt` on the current `PaymentAuthorization` (FR-011b). Makes no Stripe call. No further drafts occur. Since specs/030-company-administration (R9) it also records `CancellationReason = "owner_cancelled"`.

**Auth**: Required

**Response 204**: No content

---

## GET /payments/transactions

Returns the paginated transaction audit trail for the resident's property.

**Auth**: Required  
**Pagination**: `limit` (default 50, clamped to 1–200) and `offset` (default 0); newest first

**Query parameters**:

| Param | Type | Notes |
|-------|------|-------|
| `limit` | int | Default 50, clamped to 1–200 |
| `offset` | int | Default 0 |

**Response 200**:
```json
{
  "items": [
    {
      "id": "uuid",
      "createdAt": "2026-06-06T14:23:00Z",
      "grossAmount": 128.95,
      "feeAmount": 3.87,
      "total": 132.82,
      "cumulativeRefundedAmount": 0.00,
      "status": "Succeeded",
      "paymentMethod": "Card",
      "maskedMethod": "Visa •• 4242",
      "isRecurring": false
    }
  ],
  "totalCount": 1,
  "limit": 50,
  "offset": 0
}
```

**Note**: `maskedMethod` comes from the transaction's receipt (e.g. `Visa •• 4242`); before a receipt exists it is the generic `Card` or `ACH`. Raw payment method details are never stored.

---

## GET /payments/alert-preferences

Returns the resident's payment alert opt-in preferences.

**Auth**: Required

**Response 200**:
```json
{
  "smsOptIn": false,
  "emailOptIn": false,
  "alertPhone": null
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
  "alertPhone": "+15105551234"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `smsOptIn` | bool | Yes | |
| `emailOptIn` | bool | Yes | |
| `alertPhone` | string | Conditional | E.164; required when `smsOptIn = true` unless a phone is already on file |

**Validation**:
- `smsOptIn = true` with no phone in the request or on file → `422`
- `alertPhone`, when given, must be E.164 → `422`
- Email alerts go to the owner's email on file (`Owner.Email`, editable via `PATCH /property/owner`); there is no separate alert email.
- Each channel whose state changes appends an `AlertConsent` row (FR-031).

**Response 200**: Same shape as `GET /payments/alert-preferences`

---

## POST /payments/webhooks/stripe

Stripe webhook endpoint. Unauthenticated by JWT — authenticated by Stripe signature verification.

**Auth**: Stripe `Stripe-Signature` header (HMAC-SHA256 with `Stripe:WebhookSigningSecret`; timestamp tolerance `Stripe:WebhookToleranceSeconds`, default 300)  
**Content-Type**: `application/json` (raw body required for signature verification)  
**Rate limit**: None (Stripe IPs only — consider Cloudflare WAF allowlist)

**Processing**:
1. Read raw body bytes.
2. Verify `Stripe-Signature` via `EventUtility.ConstructEvent` with timestamp tolerance (FR-030); return `400` on failure **without mutating data**.
3. Durably capture into `WebhookEventInbox` by `StripeEventId` (unique); if it already exists and is `Processed`, return `200` immediately (duplicate); a `Received` row is processed again. Processing runs in the same request after the capture is saved, and `200` is returned whether processing succeeds or fails (a failure leaves the row `Received` for the reconcile retry) (FR-032).
4. Route by `event.Type`:
   - `payment_intent.succeeded` → transition `PaymentTransaction` to Succeeded; write `LedgerEntry` if was Pending (ACH settlement)
   - `payment_intent.payment_failed` → transition to Failed; store `FailureCode` + `FailureMessage`; trigger alert if recurring + opted in
   - `charge.refunded` → `PartiallyRefunded`/`Refunded` from the cumulative `amount_refunded`; compensating `Refund` ledger entry
   - `charge.dispute.created` → `Disputed`; compensating `Chargeback` entry
   - `charge.dispute.closed` → won: `Succeeded` plus a restoring entry; lost: `DisputeLost` (plus the NSF fee when enabled)
   - `payment_intent.payment_failed` on an already-`Succeeded` ACH transaction → `Returned` (see webhook-events.md)
   - *(all others)* → log at `Information`; do not mutate
5. Mark the `WebhookEventInbox` row `Processed`; on processing failure, leave it for retry / dead-letter (FR-032).
6. Alerts/receipts are enqueued to `OutboxMessage` in the same transaction and dispatched promptly (FR-034).

**Response**: `200 OK` (always, unless signature verification fails → `400`). Body: `{}` or empty.

**SLA**: Must acknowledge within Stripe's 30-second timeout. Heavy processing must not block acknowledgement.

---

## GET /payments/statements

Per-owner account statement (charges, payments, running balance, credits) for the resident's
property — supports NC owner record-inspection rights (FR-039, § 47F-3-118).

**Auth**: Required (still readable when the community is Archived, 030 FR-012a) · not paginated

**Query**: `startDate`, `endDate` (optional, ISO `yyyy-MM-dd`); the window defaults to the trailing 12 months.

**Response 200**:
```json
{
  "startDate": "2025-06-06",
  "endDate": "2026-06-06",
  "openingBalance": 90.00,
  "totalCharges": 35.00,
  "totalPayments": 90.00,
  "closingBalance": 35.00,
  "lines": [
    { "entryDate": "2026-06-01", "documentNumber": null, "description": "June assessment", "chargeAmount": 35.00, "paymentAmount": 0.00, "runningBalance": 125.00, "entryType": "RegularAssessment" },
    { "entryDate": "2026-06-06", "documentNumber": null, "description": "Online Payment – Card – ch_…", "chargeAmount": 0.00, "paymentAmount": 90.00, "runningBalance": 35.00, "entryType": "Payment" }
  ]
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
  "asOf": "2026-06-06",
  "totalDue": 35.00,
  "creditBalance": 0.00,
  "breakdown": [
    { "category": "RegularAssessment", "amount": 35.00 }
  ]
}
```

---

## GET /payments/receipts/{id}

Retrieve a durable payment receipt (FR-007f). Issued at card success / ACH settlement.

**Auth**: Required (the receipt must belong to the caller's property). `{id}` is the receipt id (`receiptId` from the confirm response), not the transaction id.

**Response 200**:
```json
{
  "id": "uuid",
  "transactionId": "uuid",
  "confirmationNumber": "CONF-A1B2C3D4E5",
  "maskedMethod": "Visa •• 4242",
  "grossAmount": 128.95, "feeAmount": 3.87, "total": 132.82,
  "issuedAt": "2026-06-06T14:23:05Z"
}
```
**Errors**: `404` — no such receipt for the caller's property.

---

## POST /payments/jobs/run-drafts  *(internal — Cloud Scheduler)*

Processes auto-pay drafts due today (FR-010). Charges vaulted methods off-session, writes
recurring `PaymentTransaction`s + `DraftEntry`s, sends variable-amount notices (FR-011c), and
enqueues outbox messages.

**Auth**: `X-Scheduler-Secret` header, compared in constant time with `Jobs:SchedulerSharedSecret` (`401` otherwise); **not** session-auth; no OIDC path. Optional `?date=YYYY-MM-DD` overrides today (UTC). Source-
restricted via Cloudflare. **Idempotency**: per draft `draft:{recurringId}:{period}` (FR-011d)
— safe to re-run.

**Response 200**: `{ "dueCount": 12, "charged": 11, "failed": 1, "skipped": 0, "noticesSent": 2 }`

---

## POST /payments/jobs/reconcile  *(internal — Cloud Scheduler)*

Reconciliation sweep (FR-033) + outbox flush (FR-034) + webhook-inbox retry (FR-032). Polls
Stripe for charge/event status and resolves any transaction left non-terminal past its window
(e.g. ACH `Pending`), catching missed webhooks.

**Auth**: same as `run-drafts`.

**Response 200**: `{ "resolvedAchTransactions": 3, "retriedWebhooks": 1, "dispatchedAlerts": 5 }`

---

## Breaking Change Notes

- `POST /payments/one-time` (old endpoint) is replaced by the two-step `POST /payments/intent` + `POST /payments/one-time/confirm`. Old frontend code must be updated.
- `PUT /payments/recurring` request body changes: removes raw card/bank fields, adds `setupIntentId` and `mandateAccepted`. Existing frontend code for recurring setup must be replaced with the SetupIntent flow.
- `GET /payments/recurring` response changes: removes masked card/bank fields, adds `maskedMethod`, `mandateAcceptedAt`, `nextDraftDate`, `nextDraftAmount`.

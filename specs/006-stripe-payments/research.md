# Research: Stripe Payments (One-Time & Recurring)

**Feature**: 006-stripe-payments | **Date**: 2026-06-06

---

## 1. Stripe SDK (.NET)

**Decision**: Use `Stripe.net` NuGet package (≥ 47.x). Register via `services.AddStripe(config["Stripe:SecretKey"])` (extension from `Stripe.net`). All Stripe API objects are accessed through typed service clients (e.g., `PaymentIntentService`, `SetupIntentService`, `CustomerService`).

**Rationale**: Official Stripe SDK for .NET. Provides typed request/response objects, automatic retry, and `EventUtility.ConstructEvent` for webhook signature verification.

**Alternatives considered**:
- Direct HTTP calls — rejected: error-prone, no retry, no typed models.
- Third-party wrappers — rejected: unnecessary indirection; `Stripe.net` is the recommended path.

---

## 2. Stripe Payment Element (Angular)

**Decision**: Use `@stripe/stripe-js` (official Stripe.js ESM package) combined with `ngx-stripe` (Angular wrapper). Initialize once in `AppModule`/`AppComponent` with `publishable_key`; embed `<ngx-stripe-payment>` or `<ngx-stripe-elements>` in the one-time payment component.

**One-time payment flow**:
1. Frontend calls `GET /payments/options` → receives `currentBalance`, `nextAssessment`, `nextDueDate`, `cardSurchargeRate`.
2. Resident selects amount preset; frontend computes `amount` + `fee`.
3. Frontend calls `POST /payments/intent` → backend creates Stripe `PaymentIntent` (amount includes fee), returns `{ clientSecret, paymentIntentId, amount, fee, total }`.
4. Frontend renders `<ngx-stripe-payment [clientSecret]="clientSecret">`.
5. Resident fills payment details and clicks Pay; frontend calls `stripe.confirmPayment({ elements, confirmParams })`.
6. On `stripe.js` success redirect/return, frontend calls `POST /payments/one-time/confirm` with `{ paymentIntentId }`.
7. Backend fetches `PaymentIntent` from Stripe, writes `PaymentTransaction` (Succeeded for card, Pending for ACH), writes `LedgerEntry` for card, returns confirmation.
8. Stripe also sends lifecycle webhooks (handles ACH confirmation, failures, disputes).

**Rationale**: Embedded Payment Element (Spec "Idea B") keeps the HOA in PCI SAQ A scope — card data never touches HOA servers. `ngx-stripe` avoids manual DOM lifecycle management.

**Alternatives considered**:
- Stripe Checkout redirect ("Idea A") — rejected per spec.
- Stripe Customer Portal ("Idea C") — rejected per spec.
- Plain `@stripe/stripe-js` without `ngx-stripe` — viable but requires manual element lifecycle; `ngx-stripe` is lighter and Angular-native.

---

## 3. Stripe SetupIntent (Recurring / Vaulted Method)

**Decision**: Use Stripe `SetupIntent` API to vault a payment method without immediately charging. Backend creates a `Customer` (reusing if `Owner.StripeCustomerId` is already set) and a `SetupIntent` attached to that customer. Frontend renders the Payment Element in setup mode (`stripe.confirmSetup()`). On success, the `payment_method` ID is returned; backend stores it in `RecurringPayment.VaultedPaymentMethodId`.

**Recurring draft flow**:
- On draft day, backend calls `PaymentIntentService.CreateAsync` using the stored `Customer.Id` + `PaymentMethodId` with `Confirm = true` and `OffSession = true`.
- Result is recorded as a recurring `PaymentTransaction`.

**Customer creation/reuse**:
- If `Owner.StripeCustomerId` is null → create new `Customer` with `email` metadata only; store the `customer.Id` on `Owner`.
- If not null → reuse; never create duplicate.

**Mandate acceptance**:
- Frontend captures checkbox acceptance; `RecurringPayment.MandateAcceptedAt` is set at save time.
- Mandate text from spec: "I agree to the recurring ACH mandate — charged on the selected day each month until auto-pay is turned off."

**Rationale**: HOA controls scheduling and uses vaulted methods for off-session charges. Stripe-native subscriptions are explicitly out of scope.

---

## 4. Stripe Webhook Signature Verification

**Decision**: `StripeWebhookEndpoint` reads raw request body (`Request.Body` as `Stream`), extracts `Stripe-Signature` header, calls `EventUtility.ConstructEvent(rawBody, signature, webhookSigningSecret)`. Any `StripeException` rejects the request with HTTP 400 **without** mutating data.

**Idempotency**:
- `WebhookEventInbox` table stores `StripeEventId` (unique), event type, scrubbed payload, and processing status (Received/Processed/DeadLettered).
- Before processing any event, check if `StripeEventId` already exists; if yes, return 200 immediately.
- Additionally guard on terminal transaction status: if a transaction is already Succeeded/Failed/Refunded/Disputed, skip status re-application.

**Unhandled event types**: Log at `Information` level with event type and ID; return 200.

**Unknown transaction references**: Log at `Warning` level; return 200 without mutating data.

**Rationale**: Stripe guarantees at-least-once delivery. Dual idempotency (event ID + terminal state guard) prevents double-writes and double-alerts.

---

## 5. Card-Fee Model (compliance-aware)  *(updated)*

**Decision**: Model the card fee per-HOA via `HoaPaymentConfig` with three knobs (FR-004b):
`CardFeeType` (Flat | Percentage), `CardScope` (AllCards | CreditOnly), and
`SurchargingEnabled` (per-jurisdiction gate). The fee is computed **server-side** and included
in the Stripe `PaymentIntent.Amount` (cents). Gross and fee are stored as **two distinct
fields** on `PaymentTransaction` (`GrossAmount`, `FeeAmount`); the fee is booked as its own
fee-income `LedgerEntry` line (FR-004d).

**Instrument rules** (enforced by config validation + at charge time):
- A **percentage** fee is a credit-card *surcharge* → `CardScope = CreditOnly`. Debit/prepaid
  are **never** percentage-surcharged. The combination Percentage + AllCards is rejected.
- A **flat** fee is a *convenience fee* and may apply to all cards (incl. debit).
- Card funding (`credit`/`debit`/`prepaid`) comes from Stripe (`PaymentMethod.Card.Funding`),
  not inferred. Persisted on the transaction as `CardFunding`.
- ACH = free (`AchFeeValue` default 0).

**Calculation**: percentage → `fee = Round(gross * rate, 2, AwayFromZero)`; flat → `fee = value`.
`total = gross + fee`.

**Refund of fee** (FR-004d): retained on refund (mirrors Stripe keeping its processing fee),
except a full refund driven by HOA/processing error returns the fee.

**Default ships safe**: Percentage, CreditOnly, `SurchargingEnabled` off where restricted.
NC surcharge legality is *verify-with-counsel*; the flat convenience-fee posture is the
conservative fallback.

**Alternatives considered**: single global `CardSurchargeRate` (prior design) — rejected:
cannot express credit-only vs all-cards, flat vs percentage, or jurisdiction gating, and
risks an illegal debit surcharge. Stripe Tax / Application Fee — not applicable.

---

## 6. Twilio SMS (.NET)

**Decision**: `Twilio` NuGet package. Initialize via `TwilioClient.Init(accountSid, authToken)` in `TwilioSmsProvider`. Send via `MessageResource.CreateAsync`. Config keys: `Twilio:AccountSid`, `Twilio:AuthToken`, `Twilio:FromNumber`.

**Failure handling**: Per spec FR-022a, if send throws, catch exception, record `alert.sent` metric with `success=false`, mark OTel span as errored, do not rethrow (webhook acknowledgement must not be blocked).

**Opt-out instructions**: SMS body includes "Reply STOP to opt out."

**Rationale**: Official Twilio .NET helper library. Simple REST call for transactional SMS.

---

## 7. SendGrid Email (.NET)

**Decision**: `SendGrid` NuGet package + `SendGrid.Extensions.DependencyInjection`. Register via `services.AddSendGrid(o => o.ApiKey = config["SendGrid:ApiKey"])`. Send via `ISendGridClient.SendEmailV3Async`. Config keys: `SendGrid:ApiKey`, `SendGrid:FromEmail`, `SendGrid:FromName`.

**Failure handling**: Same as Twilio — catch exception, record metric + errored span, do not rethrow.

**Rationale**: Official SendGrid .NET SDK. DI-friendly; easy to mock in tests via `ISendGridClient`.

---

## 8. PII Scrubbing in OTel

**Decision**: Extend existing `TelemetryScrubbingProcessor` (already present in `Infrastructure/Observability/`) to also strip Stripe payment-method IDs and customer IDs from span attributes if they appear alongside PII patterns. Payment amounts (decimals/integers) are permitted in telemetry.

**Fields to exclude from all spans/metrics/exceptions**:
- Card numbers, bank routing/account numbers, PANs
- Cardholder name, email, phone
- Billing address details

**Fields permitted**: Payment amount, currency, transaction ID (internal UUID), Stripe event type.

**Rationale**: Spec FR-025 explicitly prohibits PII in telemetry. Existing scrubber is the hook; extend it rather than duplicate.

---

## 9. Angular Stripe.js Integration Patterns

**Decision**: Load `@stripe/stripe-js` once at app bootstrap with `loadStripe(publishableKey)` (lazy-loads the Stripe.js script). In `PaymentsService`, expose `stripe$: Observable<Stripe | null>`. Payment components inject `PaymentsService` to get the Stripe instance and mount Elements.

**Environment config**: Stripe publishable key stored in `environment.ts` (public key, safe to commit). Stripe secret key is backend-only.

**ngx-stripe version**: Match `@stripe/stripe-js` major version to avoid SDK mismatch.

**Rationale**: Stripe.js must be loaded from `js.stripe.com` for PCI compliance (not bundled). `loadStripe` defers loading; `ngx-stripe` wraps lifecycle properly for Angular change detection.

---

## 10. Amount Preset Resolution

**Decision**: `GET /payments/options` endpoint (authenticated, property-scoped) returns:

```json
{
  "currentBalance": 125.00,
  "nextAssessment": 35.00,
  "nextAssessmentDueDate": "2026-07-01",
  "cardSurchargeRate": 0.03
}
```

Frontend computes presets:
- "Current balance" → `currentBalance`
- "Next assessment" → `nextAssessment`
- "Balance + next" → `currentBalance + nextAssessment`
- "Other" → resident enters custom amount

Backend derives `currentBalance` from the latest `LedgerEntry.RunningBalance` for the property. `nextAssessment` from `Property.MonthlyAssessment`.

**Rationale**: Keeps amount resolution server-side (authoritative), avoids a separate microservice, and reuses existing ledger data.

---

## 11. DraftEntry ↔ PaymentTransaction Linkage

**Decision**: Add `TransactionId` (nullable `Guid`) FK column to `DraftEntries`. When a scheduled draft runs, it creates both a `DraftEntry` and a `PaymentTransaction`; the `DraftEntry.TransactionId` links to the `PaymentTransaction`. This allows the drafts table to surface status from the audit trail.

**Rationale**: Spec FR-010b requires the drafts table to show status (Paid/Scheduled). Linking to `PaymentTransaction` avoids duplicating status in two tables.

---

## 12. RecurringAmountType Resolution at Draft Time

The existing `RecurringAmountType` enum has three **string-persisted** values — reuse them
as-is (do not rename; see data-model N2/N3):

| Enum Value | UI Label | Draft Amount |
|------------|----------|-------------|
| `Assessment` | "Just the assessment" | `Property.MonthlyAssessment` |
| `Balance` | "Whatever I owe" | Recomputed open balance from the append-only ledger (floor at 0) |
| `Fixed` | "A fixed amount I pick" | `RecurringPayment.FixedAmount` |

Resolution happens at draft execution time, not at setup time. (`Property` already carries
`MonthlyAssessment`, `AnnualAssessment`, `AssessmentDueDay`, `LateFeeAmount`,
`LateFeeGraceDays`, `FinanceChargeRate` — reuse these; no new assessment/late-fee fields.)

---

## 13. Migration Strategy

**Decision**: Single EF Core migration covering:
1. `ALTER TABLE Owners ADD` — `StripeCustomerId`, `AlertSmsOptIn`, `AlertEmailOptIn`, `AlertPhone`
2. `ALTER TABLE RecurringPayments ADD` — `VaultedPaymentMethodId`, `MandateAcceptedAt`
3. `ALTER TABLE RecurringPayments DROP` — `RoutingNumberMasked`, `AccountNumberMasked`, `AccountType`, `CardNumberMasked`, `CardExpiry`, `CardholderName`, `BillingZip`
4. `CREATE TABLE PaymentTransactions`
5. `CREATE TABLE WebhookEventInbox` (durable intake + dead-letter), plus `OutboxMessages`, `PaymentAuthorizations`, `AlertConsents`, `HoaPaymentConfigs`, `Receipts`
6. `ALTER TABLE DraftEntries ADD TransactionId UUID NULL REFERENCES PaymentTransactions(Id)`

All nullable-add and drop columns in one migration applied idempotently at Cloud Run startup. No destructive data loss (existing rows get nulls for new columns; dropped columns were only masked data, no financial value).

**Rollback plan**: Reverse migration available; the dropped masked fields are redundant once replaced by vault references.

---

# Phase 0 Addendum — Compliance & Recovery Decisions

> Added for the expanded spec. These supersede/extend the relevant items above where noted.

## 14. Scheduled Drafts & Reconciliation on Cloud Run (scale-to-zero)

**Decision**: No in-process `BackgroundService`/timer. A **Google Cloud Scheduler** job
invokes authenticated internal endpoints on a cron:
- `POST /payments/jobs/run-drafts` — processes the day's due auto-pay drafts (FR-010).
- `POST /payments/jobs/reconcile` — the reconciliation sweep (FR-033) + outbox flush.

**Auth**: Cloud Scheduler sends an OIDC token (verified by the API) or a shared secret header,
and the endpoints sit behind Cloudflare with source restrictions. Not session-authenticated.

**Idempotency**: each draft uses a deterministic key `draft:{recurringId}:{period}` (FR-011d)
so an overlapping/re-run job cannot double-draft. The job is safe to re-run.

**Rationale**: scale-to-zero means in-process schedulers will not fire reliably; Cloud
Scheduler is the platform-native trigger. **Alternative rejected**: an always-on worker
service contradicts the infra mandate and adds cost.

## 15. Payment Allocation (statutory order)

**Decision**: `AllocationService` applies a payment across open charges by category priority
from `HoaPaymentConfig.AllocationOrderJson`, default assessments (oldest→newest) → late fees →
finance charges/interest → other (FR-007b, NC declaration-driven, satisfies CA §5655-style
rules). Charges and payments are `LedgerEntry` rows; allocation runs under a per-property lock
to assign the next `Sequence` deterministically. Overpayment surplus becomes a `Credit` entry
(balance may go negative, FR-007c) auto-applied to future charges.

**Rationale**: partial/open-balance/custom payments are now first-class; a single running
balance cannot honor statutory order or age receivables.

## 16. Append-Only Ledger & Deterministic Balance

**Decision**: `LedgerEntry` gains `Sequence` (per-property monotonic) + `CreatedAtUtc`.
`RunningBalance` is recomputed ordered by `Sequence`, never by `EntryDate` (a `DateOnly`).
Entries are immutable; refunds/returns/disputes/credits are **compensating** entries
(FR-007d/e, FR-014a/b). This fixes the prior `latest-row − amount` math under out-of-order ACH
settlements, refunds, and concurrent webhooks (SC-009). Each payment/reversal/fee entry carries
`TransactionId` for transaction↔ledger reconciliation.

## 17. Refund / Dispute / ACH-Return Lifecycle  *(extends §4 webhook handling)*

**Decision**:
- **Partial refunds** (FR-014b): driven by `charge.refunded` / `charge.refund.updated`; use
  `charge.amount_refunded` as the cumulative source of truth → set `CumulativeRefundedAmount`;
  status `PartiallyRefunded` until it equals `Total`, then `Refunded`. Each delta writes a
  compensating ledger entry.
- **Dispute resolution** (FR-014d): `charge.dispute.created` → `Disputed` + reversal;
  `charge.dispute.closed` → won restores funds (re-reverse, back to `Succeeded`), lost →
  `DisputeLost` (reversal stands) + optional NSF/return fee.
- **ACH return after settlement** (FR-014c): a previously `Succeeded` `us_bank_account` charge
  that later returns surfaces as a charge-failure event. **Confirm exact Stripe event during
  implementation** (`charge.failed` vs a `payment_intent.payment_failed` on the settled intent
  vs a `charge.refunded` with reason); the reconciliation sweep (§19) is the backstop if the
  event is missed. On return → status `Returned` (store `ReturnCode`), compensating ledger
  entry, alert if recurring+opted-in, and an NSF fee when `HoaPaymentConfig.NsfFeeEnabled`
  (FR-014e).

## 18. Idempotent Payment Initiation

**Decision**: intent/confirm/setup endpoints accept a client-generated `Idempotency-Key`
header (FR-007a), persisted on `PaymentTransaction.IdempotencyKey` (unique) and forwarded to
Stripe via `RequestOptions.IdempotencyKey`. A repeat returns the original result. Survives
restarts (durable in PostgreSQL, FR-035).

## 19. Durable Webhook Intake, Outbox & Reconciliation

**Decision** (supersedes §4 ack-timing):
- **Durable intake** (FR-032): verify signature → persist `WebhookEventInbox` (Received) →
  **then** ack 200. Processing runs after capture; failures increment `Attempts`, retry, and
  `DeadLettered` after a threshold. No verified event is lost on crash/scale-to-zero.
- **Replay protection** (FR-030): `EventUtility.ConstructEvent` enforces signature timestamp
  tolerance in addition to `StripeEventId` dedupe.
- **Outbox** (FR-034): alert/receipt sends are written as `OutboxMessage` rows in the same DB
  transaction as the status change, then dispatched **promptly** — `OutboxDispatcher` is invoked
  in-process immediately after the webhook is acknowledged so failure alerts meet SC-006
  (≤5 min); the reconcile job only sweeps anything still `Pending`. Exactly-the-intended-once;
  provider rejection is not retried (FR-022a).
- **Reconciliation sweep** (FR-033): periodically list Stripe charges/events and resolve any
  transaction non-terminal past its window (e.g. ACH still `Pending`). Stripe is the external
  system-of-record.

## 20. NACHA & TCPA

**Decision**:
- **Mandate record** (FR-011b): persist `PaymentAuthorization` (text+version, timestamp, IP,
  UA, amount/draft-day terms; `StripeMandateId` when available). Retain ≥2yr past
  `TerminatedAt`.
- **Variable-amount notice** (FR-011c): for `Balance` (open-balance) schedules, the reconcile/run-drafts
  job sends an advance notice of the upcoming amount `VariableNoticeLeadDays` before the draft.
- **TCPA consent** (FR-031): every opt-in/opt-out appends `AlertConsent` with text/version +
  timestamp; STOP/opt-out immediately and durably disables the channel.

## 21. Settlement Reconciliation Data

**Decision** (FR-037): on `payment_intent.succeeded`/`charge.updated`, expand
`balance_transaction` to capture `StripeBalanceTransactionId`, `ProcessorFeeAmount` (Stripe's
own fee, distinct from the resident-facing `FeeAmount`), and `StripePayoutId`. This lets the
gross-at-charge ledger reconcile to Stripe's batched net payouts. `FundCode` on ledger entries
(FR-038) is a forward hook for operating/reserve/fee-income GL mapping (full fund accounting
out of scope).

## 22. Receipts (FR-007f)

**Decision**: on card success / ACH settlement, write a `Receipt` row + enqueue a
`receipt_email` outbox message; expose retrieval via the portal. Rendered on demand from
`RenderModel` (no large blob storage by default; if PDFs are persisted they use R2/MinIO).

## 23. Statements (FR-039, NC § 47F-3-118)

**Decision**: `GET /payments/statements` (per-owner: charges, payments, running balance,
credits) and `GET /payments/unpaid-assessments` (payoff/statement-of-unpaid-assessments) read
from the append-only ledger. Supports NC owner record-inspection / payoff rights.

## 24. Backups, RPO/RTO & Reconstruction (FR-036)

**Decision**: rely on Neon PITR for ledger/transactions/authorizations; document RPO/RTO in
ops config. Because Stripe holds authoritative charge history and transactions store
`pi_/ch_/txn_/po_` references, the transaction trail is reconstructable from Stripe + stored
references in a disaster. Idempotency keys and the inbox are durable across restarts (FR-035).

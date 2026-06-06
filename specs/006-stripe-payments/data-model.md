# Data Model: Stripe Payments (One-Time & Recurring)

**Feature**: 006-stripe-payments | **Date**: 2026-06-06

> Updated for the expanded spec (clarify + compliance/recovery passes): gross/fee split,
> cumulative refunds, settlement references, new lifecycle statuses, append-only ledger with
> deterministic ordering and new entry types, statutory allocation, overpayment credits,
> mandate + TCPA consent records, durable webhook inbox, transactional outbox, and per-HOA
> payment configuration.

---

## New Entities

### PaymentTransaction

**Purpose**: Immutable audit record for every payment attempt — one per charge/setup outcome
(SC-003). Distinct from the accounting `LedgerEntry`, and referenced by the ledger entries it
produces (FR-007e, FR-012).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `PropertyId` | `Guid` | No | FK → `Properties.Id`; HOA tenant boundary |
| `OwnerId` | `Guid` | No | FK → `Owners.Id` |
| `StripePaymentIntentId` | `string` | Yes | `pi_...`; null for setup-only |
| `StripeChargeId` | `string` | Yes | `ch_...` |
| `GrossAmount` | `decimal(10,2)` | No | Principal before fee (FR-004b) |
| `FeeAmount` | `decimal(10,2)` | No | Convenience fee / surcharge; 0 for ACH |
| `Total` | `decimal(10,2)` | No | `GrossAmount + FeeAmount`; amount charged |
| `CumulativeRefundedAmount` | `decimal(10,2)` | No | Default 0; accumulates partial refunds (FR-014b) |
| `Currency` | `string(3)` | No | `"usd"` |
| `Status` | `TransactionStatus` | No | see enum below |
| `PaymentMethod` | `PaymentMethod` | No | Ach, Card (existing enum, reused) |
| `CardFunding` | `CardFunding` | Yes | credit/debit/prepaid/unknown from `card.funding` (FR-004b) |
| `FailureCode` | `string` | Yes | Stripe decline/return code |
| `FailureMessage` | `string` | Yes | PII-scrubbed reason |
| `ReturnCode` | `string` | Yes | ACH/NACHA return code (R01/R02…) (FR-014c) |
| `IsRecurring` | `bool` | No | true if from a scheduled draft |
| `IdempotencyKey` | `string` | Yes | Client-supplied initiation key (FR-007a); unique |
| `StripeBalanceTransactionId` | `string` | Yes | `txn_...` for reconciliation (FR-037) |
| `ProcessorFeeAmount` | `decimal(10,2)` | Yes | Stripe's own fee (distinct from `FeeAmount`) (FR-037) |
| `StripePayoutId` | `string` | Yes | `po_...` payout the funds settled into (FR-037) |
| `Metadata` | `string` | Yes | JSON; no PII |
| `CreatedAt` | `DateTimeOffset` | No | UTC; set once |
| `UpdatedAt` | `DateTimeOffset` | No | UTC; on status transitions |

**Constraints / indexes**:
- `StripePaymentIntentId` unique (filtered, where not null) — one transaction per intent.
- `IdempotencyKey` unique (filtered, where not null) — collapse double-submits (FR-007a).
- Indexes: `PropertyId`, `OwnerId`, `StripeChargeId`, `Status`, `CreatedAt` (pagination).
- Terminal-status guard used for webhook idempotency (FR-017).

---

### PaymentAuthorization (mandate record)

**Purpose**: Immutable NACHA/dispute-defense record of a recurring authorization (FR-011b).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `RecurringPaymentId` | `Guid` | No | FK → `RecurringPayments.Id` |
| `MandateText` | `string` | No | Exact text shown |
| `MandateVersion` | `string` | No | Version identifier of the mandate template |
| `AmountTermsSnapshot` | `string` | No | Agreed amount-type/fixed value + draft day at acceptance |
| `AcceptedAt` | `DateTimeOffset` | No | UTC |
| `AcceptedIp` | `string` | Yes | Source IP (encrypted at rest) |
| `AcceptedUserAgent` | `string` | Yes | UA string |
| `StripeMandateId` | `string` | Yes | Stripe `mandate_...` when available |
| `TerminatedAt` | `DateTimeOffset` | Yes | Set when schedule disabled; drives ≥2yr retention |

**Constraints**: append-only (never updated except `TerminatedAt`); retained ≥ 2 years past
`TerminatedAt` (FR-011b). Index: `RecurringPaymentId`.

---

### AlertConsent (TCPA proof)

**Purpose**: Proof of SMS/email consent and opt-out, sufficient for TCPA (FR-031).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `OwnerId` | `Guid` | No | FK → `Owners.Id` |
| `Channel` | `string` | No | `sms` or `email` |
| `Action` | `string` | No | `opt_in` or `opt_out` |
| `ConsentText` | `string` | Yes | Text/version shown at opt-in |
| `OccurredAt` | `DateTimeOffset` | No | UTC |
| `SourceIp` | `string` | Yes | Encrypted at rest |

**Constraints**: append-only history. Index: `OwnerId`, `Channel`.

---

### WebhookEventInbox

**Purpose**: Durable webhook intake + idempotency + dead-letter (FR-032, FR-017). Replaces the
prior `ProcessedWebhookEvent` (superset).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `StripeEventId` | `string` | No | `evt_...`; **unique** |
| `EventType` | `string` | No | e.g. `payment_intent.succeeded` |
| `Payload` | `string` | No | Verified raw event JSON, PII-scrubbed |
| `Status` | `WebhookProcessingStatus` | No | Received, Processed, DeadLettered |
| `Attempts` | `int` | No | Default 0 |
| `LastError` | `string` | Yes | Last processing error (scrubbed) |
| `ReceivedAt` | `DateTimeOffset` | No | UTC; set before 2xx ack |
| `ProcessedAt` | `DateTimeOffset` | Yes | UTC |

**Constraints**: `StripeEventId` unique. Index: `Status` (for retry/dead-letter sweep).

---

### OutboxMessage

**Purpose**: Transactional outbox for alerts and receipts so a crash neither loses nor
duplicates them (FR-034).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `Kind` | `string` | No | `sms_alert`, `email_alert`, `receipt_email` |
| `OwnerId` | `Guid` | No | FK → `Owners.Id` |
| `TransactionId` | `Guid` | Yes | FK → `PaymentTransactions.Id` |
| `PayloadJson` | `string` | No | Render inputs (no PII beyond delivery target) |
| `Status` | `OutboxStatus` | No | Pending, Sent, Failed |
| `Attempts` | `int` | No | Default 0 |
| `CreatedAt` | `DateTimeOffset` | No | UTC |
| `SentAt` | `DateTimeOffset` | Yes | UTC |

**Constraints**: written in the **same transaction** as the status change that triggers it;
dispatched by `OutboxDispatcher`. Alert sends are not retried on provider rejection (FR-022a);
a provider failure sets `Failed` and records `alert.sent{success=false}`. Index: `Status`.

---

### HoaPaymentConfig

**Purpose**: Per-HOA payment policy so fees/allocation/NSF/jurisdiction are configuration,
not code (FR-004b, FR-007b, FR-014e, NC compliance section).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `HoaId` / `AssociationId` | `Guid` | No | Tenant key |
| `CardFeeType` | `FeeType` | No | Flat or Percentage |
| `CardFeeValue` | `decimal(10,4)` | No | Flat amount or rate (e.g. 0.03) |
| `CardScope` | `CardScope` | No | AllCards or CreditOnly |
| `SurchargingEnabled` | `bool` | No | Per-jurisdiction gate (default off where restricted) |
| `AchFeeValue` | `decimal(10,4)` | No | Default 0 |
| `AllocationOrderJson` | `string` | No | Ordered category priority (FR-007b) |
| `NsfFeeEnabled` | `bool` | No | Default false |
| `NsfFeeAmount` | `decimal(10,2)` | No | Returned-payment fee (FR-014e) |
| `VariableNoticeLeadDays` | `int` | No | NACHA variable-amount notice lead (FR-011c) |

> **Late fees / finance charges already exist on `Property`** (`LateFeeAmount`,
> `LateFeeGraceDays`, `FinanceChargeRate`, `AssessmentDueDay`, `MonthlyAssessment`,
> `AnnualAssessment`). Reuse those for late-fee/interest logic and NC caps — do **not** add a
> separate `LateFeeConfigJson`. `HoaPaymentConfig` covers only the new payment-fee/allocation/
> NSF/notice policy.

**Validation (FR-004b)**: reject `CardFeeType = Percentage` combined with `CardScope = AllCards`
(percentage surcharge may target **credit only**; debit/prepaid never percentage-surcharged).

---

### Receipt

**Purpose**: Durable, retrievable payment receipt (FR-007f). Issued at card success / ACH
settlement.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `Guid` | No | PK |
| `TransactionId` | `Guid` | No | FK → `PaymentTransactions.Id` (unique) |
| `OwnerId` | `Guid` | No | FK → `Owners.Id` |
| `ConfirmationNumber` | `string` | No | Human-facing reference |
| `MaskedMethod` | `string` | No | e.g. `Visa •• 4242` |
| `GrossAmount` / `FeeAmount` / `Total` | `decimal(10,2)` | No | Snapshot |
| `IssuedAt` | `DateTimeOffset` | No | UTC |
| `RenderModel` | `string` | No | JSON used to render PDF/HTML on demand |

---

## Modified Entities

### Owner (modified)

| Column | Type | Nullable | Change |
|--------|------|----------|--------|
| `StripeCustomerId` | `string` | Yes | **NEW** `cus_...` |
| `AlertSmsOptIn` | `bool` | No | **NEW** default `false` |
| `AlertEmailOptIn` | `bool` | No | **NEW** default `false` |
| `AlertPhone` | `string` | Yes | **NEW** E.164 (encrypted at rest, FR-029) |

`AlertPhone` required when `AlertSmsOptIn = true`. Existing `Email` used for email alerts.
Consent changes also append an `AlertConsent` row.

### RecurringPayment (modified)

**Added**:

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `VaultedPaymentMethodId` | `string` | Yes | Stripe `pm_...` |
| `CurrentAuthorizationId` | `Guid` | Yes | FK → `PaymentAuthorizations.Id` (FR-011b) |

**Removed** (migration): `RoutingNumberMasked`, `AccountNumberMasked`, `AccountType`,
`CardNumberMasked`, `CardExpiry`, `CardholderName`, `BillingZip` (FR-009, SC-008).

**Retained**: `Id`, `PropertyId`, `AmountType`, `FixedAmount`, `Method` (existing `PaymentMethod` enum),
`DraftDay`, `Status`, `ProcessingFee`, `UpdatedAt`. `ProcessingFee` now derived per-method from
`HoaPaymentConfig` at draft time (FR-010d), not the hard-coded `1.95`.

### LedgerEntry (modified — append-only, deterministic)

| Column | Type | Nullable | Change |
|--------|------|----------|--------|
| `Sequence` | `long` | No | **NEW** monotonic per-property order key (FR-007d) |
| `CreatedAtUtc` | `DateTimeOffset` | No | **NEW** precise UTC timestamp (existing `EntryDate` is `DateOnly`) |
| `TransactionId` | `Guid` | Yes | **NEW** FK → `PaymentTransactions.Id` (FR-007e) |
| `FundCode` | `string` | Yes | **NEW** optional operating/reserve/fee-income GL hook (FR-038) |

`RunningBalance` is **recomputed deterministically** ordered by (`PropertyId`, `Sequence`),
not by `EntryDate` (fixes out-of-order ACH/refund inserts, SC-009). Entries are never mutated
or deleted; corrections are compensating entries (FR-014a/b, FR-014c/d, FR-007e). `EntryType`
gains new values (see enum). Overpayment surplus is a `Credit` entry (balance may go negative,
FR-007c).

### DraftEntry (modified)

| Column | Type | Nullable | Change |
|--------|------|----------|--------|
| `TransactionId` | `Guid` | Yes | **NEW** FK → `PaymentTransactions.Id`; nullable, no cascade |

---

## New / Updated Enums

### TransactionStatus (FR-012)

```csharp
public enum TransactionStatus
{
    Pending,            // ACH submitted; awaiting settlement
    Succeeded,
    Failed,
    PartiallyRefunded,  // cumulative refund < total (FR-014b)
    Refunded,           // cumulative refund == total
    Disputed,           // dispute created (FR-014)
    DisputeLost,        // dispute closed-lost; chargeback stands (FR-014d)
    Returned            // settled ACH later returned (FR-014c)
}
```

State machine:
```
[card submit] ───────────────▶ Succeeded ──┬─▶ PartiallyRefunded ─▶ Refunded
[ACH submit] ─▶ Pending ─┬─▶ Succeeded ─────┤
                         └─▶ Failed         ├─▶ Returned            (ACH return after settle)
Succeeded ─▶ Disputed ─┬─▶ Succeeded        │   (dispute won → funds restored)
                       └─▶ DisputeLost      ┘
```

### CardFunding
```csharp
public enum CardFunding { Credit, Debit, Prepaid, Unknown }
```

### FeeType / CardScope
```csharp
public enum FeeType { Flat, Percentage }
public enum CardScope { AllCards, CreditOnly }   // Percentage requires CreditOnly
```

### LedgerEntryType (extended)
```csharp
public enum LedgerEntryType
{
    RegularAssessment, Payment, LateFee, FinanceCharge,  // existing
    Refund, Reversal, Chargeback, ReturnedPaymentFee, Credit, Adjustment  // NEW (FR-007e)
}
```

### WebhookProcessingStatus / OutboxStatus
```csharp
public enum WebhookProcessingStatus { Received, Processed, DeadLettered }
public enum OutboxStatus { Pending, Sent, Failed }
```

### PaymentMethod (reuse existing — do NOT rename)
```csharp
public enum PaymentMethod { Ach, Card }  // EXISTING; string-persisted via HasConversion<string>()
```
New entities (`PaymentTransaction`, etc.) **reuse the existing `PaymentMethod` enum**. Do not
rename it to `PaymentMethodType` — `RecurringPayment.Method` is stored as the string values
`"Ach"`/`"Card"`, so a type/value rename would orphan existing rows for no benefit.

### RecurringAmountType (reuse existing values — do NOT rename)
```csharp
public enum RecurringAmountType { Assessment, Balance, Fixed }  // EXISTING; string-persisted
```
Keep the existing value names. `Assessment` = standard assessment, `Balance` = open balance
("Whatever I owe"), `Fixed` = fixed amount. Contracts/UI labels may differ ("Just the
assessment", etc.) but the **persisted enum values stay `Assessment`/`Balance`/`Fixed`** to
avoid a data migration.

---

## Entity Relationships

```
Property (1) ──< RecurringPayment (0..1) ──< PaymentAuthorization (*)
Property (1) ──< PaymentTransaction (*) ──< LedgerEntry (*)        # tx → produced ledger rows
Property (1) ──< LedgerEntry (*)                                   # incl. charges/credits
Property (1) ──< DraftEntry (*) ──> PaymentTransaction (0..1)
Property (1) ──── Owner (1) ──< AlertConsent (*)
Owner (1) ──< PaymentTransaction (*) ; Owner (1) ──< OutboxMessage (*)
PaymentTransaction (1) ──< Receipt (0..1)
HoaPaymentConfig (1 per HOA)        WebhookEventInbox (standalone idempotency/dead-letter log)
```

---

## EF Core Configuration Notes

- Enums stored as `string` via `.HasConversion<string>()`.
- Money columns `decimal(10,2)`; fee rates `decimal(10,4)`.
- `PaymentTransaction`: filtered unique indexes on `StripePaymentIntentId` and `IdempotencyKey`.
- `WebhookEventInbox.StripeEventId` unique, `HasMaxLength(255)`.
- `LedgerEntry.Sequence`: per-property monotonic (DB sequence or computed `MAX+1` under the
  property row lock used by `AllocationService`); index `(PropertyId, Sequence)`.
- `Owner.AlertPhone`, `PaymentAuthorization.AcceptedIp`, `AlertConsent.SourceIp`: encrypted at
  rest (FR-029) — provider-level (Neon) + column protection for the most sensitive.
- Nullable FKs (`DraftEntry.TransactionId`, `LedgerEntry.TransactionId`) use no cascade —
  audit/ledger rows must persist independently.
- Migration is reversible and MUST NOT drop historical ledger rows (FR-027).

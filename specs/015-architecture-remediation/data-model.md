# Data Model: Architecture Remediation

**Feature**: 015-architecture-remediation · **Date**: 2026-07-02

**No schema changes.** This feature introduces no tables, columns, or migrations. It changes *how* existing entities are written (atomicity, idempotency, serialization) and adds in-memory/contract types. Entities below are existing unless marked NEW (in-memory only).

## Existing entities (behavioral changes only)

### PaymentTransaction

Lifecycle record of one payment attempt. Key fields (existing): `Id`, `PropertyId`, `Status`, `Amount`, `CumulativeRefundedAmount`, `PaymentIntentId`, `Method`, receipt reference.

- **Invariant (strengthened, FR-001)**: any status transition that implies ledger effects commits in the same transaction as those effects. Concretely: `Succeeded` ⇒ payment ledger entry exists; `Returned` ⇒ reversal + NSF fee entries exist; refund delta applied ⇒ refund entry exists and `CumulativeRefundedAmount` matches the sum of refund entries.
- **State transitions (unchanged set, now atomic)**: `Pending → Succeeded | Failed`; `Succeeded → Returned` (ACH); `Succeeded → (refund accumulation)`; terminal states are absorbing for webhook handlers (re-processing is a no-op, FR-002).

### LedgerEntry

Append-only financial event with `RunningBalance`. No field changes.

- **Invariant (strengthened, FR-004)**: all writers — including `RecomputeBalancesAsync` — hold the per-property advisory lock, so `RunningBalance` sequences are serialized per property.
- **Validation**: entry type/amount sign rules unchanged (owned by `LedgerService`/`FeeCalculator`).

### WebhookEventInbox

Durable inbox row per provider event (`StripeEventId` unique, `Status: Received|Processed|Failed`). No field changes.

- **Invariant (strengthened, FR-002)**: `Processed` is set in the same transaction as (or strictly after) the handler's business effects; a crash can leave `Received` (safe: retried) but never `Processed`-without-effects or half-applied effects.

### OutboxMessage

Alert outbox (existing). Unchanged; enqueue continues to join the caller's transaction — which, after P1, is the `PaymentRecorder` transaction in webhook paths too.

### Owner / RecurringPayment / Receipt

Unchanged fields. `RecurringDraftService` and receipt creation route through `PaymentRecorder` (structure only).

## New types (in-memory / contract — not persisted)

### PaymentProviderEvent (NEW, FR-021)

Gateway-neutral inbound event, produced by `StripeGateway.ParseEvent`, consumed by `WebhookProcessor`.

| Field | Type | Notes |
|-------|------|-------|
| `EventId` | string | provider event id (inbox dedupe key) |
| `Kind` | enum `PaymentProviderEventKind` | `PaymentSucceeded`, `PaymentFailed`, `AchReturned`, `Refunded`, `DisputeUpdated` |
| `PaymentIntentId` | string? | correlation to `PaymentTransaction` |
| `ChargeId` | string? | |
| `Amount` / `AmountRefunded` | decimal? | major units (converted at the gateway; `/100m` lives only in `StripeGateway`) |
| `FailureReason` | string? | provider failure code/message (neutral) |
| `DisputeStatus` | string? | |
| `RawType` | string | original provider event type, for logging only |

Validation: `Kind` mapping is exhaustive at the gateway; unknown provider event types are not emitted (inbox row still recorded, marked processed-as-ignored — current behavior preserved).

### DomainException (MOVED, FR-007)

`Domain/DomainException.cs` — `Code` (stable string), `Message`, `StatusCode` (int). Identical shape; namespace move only. Serialized by the central mapping as the error envelope (see `contracts/error-envelope.md`).

### MoneyPolicy (NEW, FR-015)

Static policy holder: `ToCents(decimal) : long` (AwayFromZero), `FromCents(long) : decimal`, `Currency = "usd"`. Replaces the duplicated conversions/constants; no stored data.

### LedgerInconsistencyFinding (NEW, FR-005 — log-only)

Structured log/alert payload, never persisted: `PaymentTransactionId`, `PropertyId`, `Discrepancy` (enum: `MissingLedgerEffect`, `DuplicateLedgerEffect`, `RefundSumMismatch`), `Detail`. Emitted by `ReconciliationService.DetectLedgerInconsistenciesAsync`.

### Generated client types (NEW, FR-011)

`neko-hoa/src/app/core/api/generated-types.ts` — full request/response shape set generated from the OpenAPI document; single client-side source of truth. `core/models/index.ts` retains only app-internal view-models and re-exports; dead types (`RecurringPayment`, `DraftEntry`, `ISODate`, `LedgerEntryType`) deleted.

## Relationships (unchanged)

`Property 1—* LedgerEntry`; `Property 1—* PaymentTransaction`; `PaymentTransaction 1—0..1 Receipt`; `WebhookEventInbox` correlates to `PaymentTransaction` via `PaymentIntentId` (soft reference, unchanged).

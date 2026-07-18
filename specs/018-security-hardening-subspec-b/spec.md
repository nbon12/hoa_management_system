# Sub-Spec B: Payment & Ledger Integrity

**Feature Branch**: `018-security-hardening-subspec-b`
**Split from**: [016 umbrella](../016-security-hardening/spec.md) — shared plan/tasks/research/data-model/contracts live there.
**Created**: 2026-07-01
**Status**: Draft

## Overview

The payments subsystem is well-architected overall — signature-verified webhooks, a durable idempotent event inbox, server-authoritative amounts, strict property-scoping, and no raw card/bank data on the server. The material gaps are in **financial-record integrity on the deferred (ACH) settlement path**, where a ledger credit and its transaction-status flip are not committed atomically, creating a double-credit window under crashes, webhook redelivery, or a race with reconciliation. Two lower-severity items harden tenant isolation of idempotency keys and add a settlement-amount cross-check.

## Clarifications

### Session 2026-07-18

- Q: What should this slice build for the FR-B5 settlement review surface? → A: Backend endpoints (list-open / resolve / dismiss with role-based authz) plus a written runbook — no dedicated Angular admin UI. Amount-mismatch settlements are rare, ops-facing exceptions; an authenticated, integration-tested resolution API + runbook is in scope, a frontend surface is not.
- Q: How should the `LedgerEntries (TransactionId, EntryType)` unique-index migration handle a pre-existing historical duplicate on a deployed DB (migrations apply at Cloud Run startup)? → A: Fail loud + runbook. Plain `CREATE UNIQUE INDEX`; if a duplicate exists the migration fails and boot halts with a clear error, and a runbook guides manual per-case resolution. No pre-flight detection pass and no auto-repair (consistent with FR-B0 forward-only).
- Q: Beyond a single transaction, what authoritatively guarantees exactly-one settlement credit under webhook redelivery racing the reconciliation sweep? → A: The FR-B2 database unique index — the second settler's credit INSERT hits the unique violation, caught and collapsed to a no-op replay; correctness rests on the DB constraint, not an application lock. Webhook and reconciliation share the `SettleSucceededAsync` path, so both are covered.
- Q: How is "matches" defined for the FR-B5 settlement amount cross-check (provider reports integer cents; server computes a decimal)? → A: Exact equality in integer minor units (cents) — no tolerance/epsilon.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Credit each settled payment exactly once (Priority: P1)

When a deferred (ACH) payment settles, the owner's ledger is credited exactly once, even if the settlement notification is redelivered, the process crashes mid-way, or the reconciliation sweep runs concurrently.

**Why this priority**: Today the ledger credit is committed in its own transaction while the transaction-status flip persists separately. A crash or redelivery between the two, or a race between webhook processing and reconciliation, can write a **second** ledger credit for the same payment — corrupting owner balances and financial records. This is the one payments finding with direct financial-integrity impact.

**Independent Test**: Simulate a crash between the ledger write and the status flip, then reprocess the settlement (via redelivery and via reconciliation); the ledger shows exactly one credit for that payment. Run webhook processing and reconciliation concurrently for the same pending payment; still exactly one credit.

**Acceptance Scenarios**:

1. **Given** a pending deferred payment, **When** its settlement is processed, **Then** the ledger credit and the status transition commit together as a single atomic unit.
2. **Given** a settlement that was interrupted after the credit but before the status flip, **When** it is reprocessed by redelivery or reconciliation, **Then** no additional ledger credit is created.
3. **Given** two concurrent settlement processors for the same payment, **When** both run, **Then** at most one ledger credit results.

---

### User Story 2 - Isolate idempotency keys per tenant (Priority: P2)

An idempotency key submitted by one property can never collide with or block a different property's transaction.

**Why this priority**: The replay lookup is scoped per property, but the uniqueness constraint is global, so a key reused across properties causes a constraint violation surfaced as a server error rather than a clean replay — a cross-tenant griefing/robustness gap.

**Independent Test**: Submit the same idempotency key from two different properties; both succeed independently, and a genuine same-property replay collapses to the original result rather than erroring.

**Acceptance Scenarios**:

1. **Given** the same idempotency key used by two different properties, **When** both submit, **Then** each is processed independently with no error.
2. **Given** a repeated submission with the same key by the same property, **When** it is received, **Then** it returns the original transaction result rather than a server error.

---

### User Story 3 - Cross-check settled amount (Priority: P3)

A ledger credit is written only when the settled amount reported by the payment provider matches the server-computed expected total.

**Why this priority**: Defense-in-depth. Amounts are fixed server-side at intent creation and cannot be altered by the client today, so this is a belt-and-suspenders assertion rather than an active exploit fix.

**Independent Test**: Feed a settlement whose provider-reported received amount differs from the expected total; the ledger credit is not written and the discrepancy is flagged.

**Acceptance Scenarios**:

1. **Given** a succeeded payment whose provider-reported received amount does not equal the server-computed total, **When** settlement is processed, **Then** no ledger credit is written and the mismatch is recorded for review.

---

### Edge Cases

- Partial captures or provider-side amount adjustments (if ever enabled) must have defined handling under the amount cross-check, rather than silently blocking legitimate settlements.
- A durable uniqueness backstop must not reject legitimate compensating entries (refunds, reversals, chargebacks) that legitimately reference the same transaction with a different entry type.
- Idempotency-key constraint changes must not break the existing pass-through of keys to the payment provider.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-B0**: The remediation is **forward-only**. *(Clarified 2026-07-02: this program prevents new double-credits and does not auto-remediate any historical duplicate ledger credits that may already exist. Correcting pre-existing duplicates, if any, is handled separately and is out of scope here — no automated repair or detection pass is included.)* *(Clarified 2026-07-18: the FR-B2 `(TransactionId, EntryType)` unique-index migration is a plain `CREATE UNIQUE INDEX` with no pre-flight duplicate-detection pass; if a historical duplicate exists the migration fails and Cloud Run startup halts with a clear error, and a runbook guides manual per-case resolution — the failing index is itself the surfacing mechanism.)*
- **FR-B1**: The deferred-settlement path MUST commit the ledger credit, the transaction-status transition, and any receipt creation as a single atomic unit, so an interruption cannot leave a credit without its corresponding status change.
- **FR-B2**: The ledger MUST enforce a durable uniqueness backstop that prevents more than one settlement credit per payment (per transaction and entry type), independent of application-level guards, while still permitting distinct compensating entries.
- **FR-B3**: Settlement processing MUST be safe under webhook redelivery and under concurrent execution with the reconciliation sweep, producing exactly one credit per settled payment. *(Clarified 2026-07-18: the authoritative exactly-once guarantee is the FR-B2 database unique index, not an application-level lock or status guard. Settlement wraps the status transition + ledger credit in one transaction; a concurrent or redelivered second settler's credit INSERT hits the unique-constraint violation, which MUST be caught and collapsed to a no-op replay (leaving the first credit intact). This holds across processes/instances since correctness rests on the DB constraint. Since webhook and reconciliation share one settlement code path — `SettleSucceededAsync`, also invoked by `ResolvePendingAchAsync` — the fix applies to both by construction.)*
- **FR-B4**: Idempotency-key uniqueness MUST be scoped per tenant (property), matching the per-tenant replay lookup, and a key collision MUST be handled as a graceful replay/duplicate response rather than a server error.
- **FR-B5**: A ledger settlement credit MUST be written only when the provider-reported received amount matches the server-computed expected total; on mismatch, the credit MUST be blocked and the mismatch MUST be recorded in a **manual review queue** for human resolution. *(Clarified 2026-07-02: block + manual review queue — no auto-credit of the expected amount and no alert-only handling; a review surface/runbook is in scope.)* *(Clarified 2026-07-18: the review surface is **backend-only** — authenticated, role-authorized endpoints to list open items and resolve/dismiss them, plus a written runbook. No dedicated Angular admin UI in this slice; resolution is an ops-facing exception path, integration-tested at the API layer.)* *(Clarified 2026-07-18: "matches" means **exact equality in integer minor units** — the server-computed total is converted to minor units (cents) and compared for exact equality with the provider-reported received amount; no tolerance/epsilon. `SettlementReviewQueue.ExpectedAmount`/`ProviderAmount` record the compared values.)*
- **FR-B6**: Payment endpoints MUST read the property claim defensively and return a clean authorization error when it is absent (shared with Sub-Spec A FR-A8; owned here for payment endpoints).

### Key Entities

- **Ledger entry**: An append-only record; settlement credits are now uniquely constrained per (transaction, entry type).
- **Deferred settlement**: The ACH success path whose credit + status transition must be atomic.
- **Idempotency record**: Per-tenant unique key used to collapse duplicate submissions.
- **Settlement review queue**: A surface holding blocked settlements whose provider amount did not match the expected total, for human resolution.

### Security & Abuse Controls *(constitution subset)*

- **API contract / correctness**: Exactly-once financial effects under retries and concurrency; amounts remain server-authoritative and provider-cross-checked.
- **Database/runtime**: Atomic multi-write settlement uses a single transaction with appropriate locking; the uniqueness backstop is enforced at the persistence layer via a strict migration.
- **Auditability**: Amount mismatches and rejected duplicate settlements are recorded without exposing sensitive payment instrument data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-B1**: Under simulated crash-and-reprocess and under concurrent webhook/reconciliation processing, a settled payment results in exactly one ledger credit in 100% of test runs.
- **SC-B2**: The same idempotency key used by two different tenants succeeds for both with zero server errors; a same-tenant replay returns the original result.
- **SC-B3**: A settlement whose provider-reported received amount differs from the server-computed total by even one minor unit (cent) writes zero ledger credits and produces a review record; an exact minor-unit match settles normally — both verified by automated test.
- **SC-B4**: The card (immediate-capture) path, already atomic, remains correct and covered by tests after the changes.

## Assumptions

- The card path's existing single-transaction pattern is the reference model to extend to the ACH path.
- Compensating ledger entries (refunds/reversals/chargebacks) legitimately share a transaction id with the original payment but differ by entry type, so the uniqueness backstop is keyed on (transaction, entry type), not transaction alone.
- Provider-reported amounts are available at settlement time for the cross-check.

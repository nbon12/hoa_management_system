# Data Model: Sub-Spec B — Payment & Ledger Integrity

**Date**: 2026-07-18 · Refines the umbrella data-model (`specs/016-security-hardening/data-model.md`) with exact entity/enum names. Migration baseline: `20260607165538_OutboxDedupKey`.

## New table: `SettlementReviewQueue`

Entity `SettlementReviewItem` (table `SettlementReviewQueue`). Holds settlements blocked by an amount mismatch (FR-B5) for operator resolution.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | uuid (PK) | |
| `PropertyId` | uuid (FK → Properties) | **tenant boundary** (constitution §3) |
| `TransactionId` | uuid (FK → PaymentTransactions) | the blocked settlement |
| `ExpectedAmount` | numeric(18,2) | server-computed total (the value compared, decimal form) |
| `ProviderAmount` | numeric(18,2) | provider-reported received amount |
| `Currency` | text | ISO currency of the compared amounts |
| `Status` | text (enum) | `open` \| `resolved` \| `dismissed` — default `open` |
| `CreatedAt` | timestamptz | UTC |
| `ResolvedAt` | timestamptz? | set on resolve/dismiss |
| `ResolvedByUserId` | uuid? (FK → AspNetUsers, nullable) | operator-supplied; null allowed with a note |
| `ResolutionNote` | text? | free-text resolution rationale |

**Indexes**: `Status` (open-queue listing); `TransactionId`. `PropertyId` for tenant-scoped queries.
**Comparison note**: the actual match test is done in **integer minor units** (D-B4); `ExpectedAmount`/`ProviderAmount` persist the human-readable decimal form of the compared values for the operator.

## Changed: `LedgerEntries` — settlement uniqueness backstop (FR-B2)

- **Add** unique index `IX_LedgerEntries_TransactionId_EntryType` on `(TransactionId, EntryType)`.
  - Permits distinct compensating entries (Refund/Reversal/Chargeback each have a different `EntryType`) while preventing a **second `Payment` credit** per transaction.
  - `TransactionId` is nullable (manual/adjustment entries have none); Postgres treats NULLs as distinct, so null-`TransactionId` rows are unaffected — a partial `WHERE "TransactionId" IS NOT NULL` filter makes this explicit and is preferred.
- **Retain** existing unique `(PropertyId, Sequence)`.
- **Migration**: plain `CREATE UNIQUE INDEX` (with the `IS NOT NULL` filter). **Fails loud at Cloud Run startup** if a historical duplicate settlement credit exists (FR-B0 — no auto-repair, no pre-flight pass). Runbook: `runbooks/ledger-dup-index-failure.md`.

## Changed: `PaymentTransactions` — per-tenant idempotency (FR-B4)

- **Drop** global unique `IX_PaymentTransactions_IdempotencyKey` (filtered `WHERE "IdempotencyKey" IS NOT NULL`).
- **Create** composite unique `IX_PaymentTransactions_PropertyId_IdempotencyKey` on `(PropertyId, IdempotencyKey)` (same `IS NOT NULL` filter).
- **Migration**: drop-and-recreate = **destructive** (§3): the migration Up queries for any existing cross-tenant `IdempotencyKey` collision first (abort with a clear error if found — improbable, keys are client-generated per checkout); Down restores the global index. Rollback note in the migration file.
- App: a create-path unique violation is caught and collapsed to the existing-transaction replay response (not a 500).

## Unchanged (no schema change)

- **Atomic settlement (FR-B1/B3)** is a transaction-boundary change in `WebhookProcessor.SettleSucceededAsync` — no schema change.
- **Amount cross-check (FR-B5)** compares values already available at settlement (`AttachSettlementAsync` charge fetch + PaymentIntent-metadata server total) — no new column beyond the `SettlementReviewQueue` capture.
- **LedgerEntry**, **PaymentTransaction**, **WebhookEventInbox** columns are otherwise unchanged.

## Migration ordering (applied idempotently at startup)

1. `CreateSettlementReviewQueue` — additive new table (safe).
2. `AddLedgerSettlementUniqueIndex` — additive index; may fail loud on historical dup (runbook).
3. `RepartitionIdempotencyKeyIndex` — drop-and-recreate unique index; pre-check + rollback note (§3 destructive).

## Tenancy (constitution §3)

`SettlementReviewQueue.PropertyId` is the tenant boundary; review-list queries scope by it. The idempotency change strengthens tenant isolation (a property's key space is now independent). No cross-HOA query introduced.

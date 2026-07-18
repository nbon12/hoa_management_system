# Quickstart: Sub-Spec B — Payment & Ledger Integrity

**Branch**: `018-security-hardening-subspec-b` · Backend-only. Verify each control against a real PostgreSQL (Testcontainers in tests; local compose DB for manual checks).

## Build & test

```bash
dotnet build HOAManagementCompany/HOAManagementCompany.csproj
# Full backend suite (Testcontainers spins PostgreSQL):
dotnet test HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj --filter "Category!=Sandbox"
```

## Verify the controls (targeted test filters)

1. **Atomic + exactly-once settlement (FR-B1/B2/B3, SC-B1)**
   ```bash
   dotnet test --filter "FullyQualifiedName~SettlementAtomicityTests"
   ```
   Asserts: status+credit+receipt commit together; an interrupted-then-reprocessed settlement (redelivery and reconciliation paths) writes no 2nd credit; two concurrent settlers → exactly one credit (the second's INSERT hits `IX_LedgerEntries_TransactionId_EntryType`, caught and collapsed); a refund (different `EntryType`) on the same transaction is still allowed.

2. **Per-tenant idempotency (FR-B4, SC-B2)**
   ```bash
   dotnet test --filter "FullyQualifiedName~IdempotencyTenantTests"
   ```
   Same key from two properties both succeed; same-property replay returns the original transaction (no 500).

3. **Amount cross-check (FR-B5, SC-B3)**
   ```bash
   dotnet test --filter "FullyQualifiedName~SettlementAmountCheckTests"
   ```
   Off-by-one-cent provider amount → zero credits + one `open` `SettlementReviewQueue` row; exact minor-unit match → settles.

4. **Review endpoints (FR-B5 surface)**
   ```bash
   dotnet test --filter "FullyQualifiedName~SettlementReviewEndpointTests"
   ```
   List-open (paginated), resolve, dismiss; missing/wrong `X-Scheduler-Secret` → 401; responses `no-store`.

5. **Card path unchanged (SC-B4)** — existing `ConfirmPayment`/card settlement tests stay green.

## Ops / deploy runbooks

- **Ledger dup-index boot failure (FR-B0/B2)**: `runbooks/ledger-dup-index-failure.md`. Before deploying to Dev/prod, check for a historical duplicate that would fail the migration:
  ```sql
  SELECT "TransactionId", "EntryType", COUNT(*)
  FROM "LedgerEntries"
  WHERE "TransactionId" IS NOT NULL
  GROUP BY "TransactionId", "EntryType"
  HAVING COUNT(*) > 1;
  ```
  Zero rows → the migration applies cleanly. Any rows → resolve per the runbook before deploy (no auto-repair).

- **Idempotency index repartition (FR-B4, §3 destructive)**: the migration pre-checks for cross-tenant key collisions:
  ```sql
  SELECT "IdempotencyKey", COUNT(DISTINCT "PropertyId")
  FROM "PaymentTransactions"
  WHERE "IdempotencyKey" IS NOT NULL
  GROUP BY "IdempotencyKey"
  HAVING COUNT(DISTINCT "PropertyId") > 1;
  ```
  Zero rows expected (keys are client-generated per checkout). Rollback note in the migration restores the global index.

- **Settlement review resolution (FR-B5)**: `runbooks/settlement-review-resolution.md` — how an operator lists, investigates, and resolves/dismisses a mismatch, and how any corrective ledger action is taken deliberately (the resolve endpoint does not credit the ledger).

## Manual smoke (optional, local)

Trigger a pending-ACH settlement via a simulated `payment_intent.succeeded` and confirm one credit; replay the same webhook event id and confirm still one credit.

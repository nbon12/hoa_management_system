# Runbook: LedgerEntries unique-index migration failure (FR-B0 / FR-B2)

**When**: A deploy halts at Cloud Run startup because the `AddLedgerSettlementUniqueIndex` migration failed with a unique-constraint violation on `IX_LedgerEntries_TransactionId_EntryType`. This means a **historical duplicate settlement credit** already exists (two `Payment` entries for the same `TransactionId`). Sub-Spec B is forward-only (FR-B0) and does **not** auto-repair; the failing index is the surfacing mechanism.

## Impact
- The new revision will not start; Cloud Run keeps the prior healthy revision serving 100% traffic. No user-facing outage.
- No data is changed by the failed migration (it is a single `CREATE UNIQUE INDEX`).

## Diagnose

Run against the target database (Dev/prod):

```sql
SELECT "TransactionId", "EntryType", COUNT(*) AS n, ARRAY_AGG("Id") AS entry_ids
FROM "LedgerEntries"
WHERE "TransactionId" IS NOT NULL
GROUP BY "TransactionId", "EntryType"
HAVING COUNT(*) > 1
ORDER BY n DESC;
```

Each row is a duplicate group. For a duplicate `Payment` group, inspect the entries and the parent transaction:

```sql
SELECT le."Id", le."Sequence", le."PaymentAmount", le."RunningBalance", le."CreatedAt", le."Description"
FROM "LedgerEntries" le
WHERE le."TransactionId" = '<transaction-id>' AND le."EntryType" = 'Payment'
ORDER BY le."Sequence";
```

## Resolve (per case — no bulk automation)

1. Confirm which credit is the legitimate one (earliest `Sequence` / matching receipt / matching payout).
2. The ledger is append-only: **do not hard-delete**. Post a **compensating Reversal entry** (EntryType=`Reversal`, negative of the erroneous credit) via the normal ledger service so balances net correct and the audit trail is preserved. The Reversal has a different `EntryType`, so it does not conflict with the new unique index.
3. Re-run `EXPLAIN`/the diagnose query to confirm the offending `(TransactionId, 'Payment')` group is now a single row (the duplicate credit still exists historically but is netted by the reversal — the unique index is on live rows, so if a true duplicate row remains, escalate to a data-fix change request rather than deleting under time pressure).
4. If a genuine duplicate **row** must be removed (not just netted) to satisfy the index, do it as a reviewed, backed-up, separately-approved data migration — never ad hoc during a deploy.
5. Redeploy. The migration now applies cleanly.

## Prevent
- The `quickstart.md` pre-deploy check queries for duplicates before every Dev/prod deploy of this slice; run it in the release checklist so the failure is caught before the rollout, not during it.

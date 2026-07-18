# Contract: Settlement Review Queue — Sub-Spec B (FR-B5)

Backend-only operator surface for settlements blocked by an amount mismatch (D-B4/D-B5). **No frontend** in this slice. All responses use the existing `{ code, message }` error shape and are `Cache-Control: no-store` (authenticated financial data). Amounts are echoed as decimals; the block decision itself is exact-minor-unit equality.

**Authorization (D-B6)**: gated by the operator shared secret `X-Scheduler-Secret` (`Jobs:SchedulerSharedSecret`, validated options — same mechanism as `POST /payments/jobs/reconcile`). Not resident-accessible; no role claim exists in the system today. Missing/wrong secret → `401`.

## GET /api/v1/payments/settlement-review
- **Auth**: operator secret.
- **Query**: `limit` (default 50, max 200), `offset` (default 0), optional `status` (default `open`).
- **Response 200**: `{ items: [{ id, propertyId, transactionId, expectedAmount, providerAmount, currency, status, createdAt }], total, limit, offset }` — paginated per constitution §4.
- **Errors**: `401` (secret).

## POST /api/v1/payments/settlement-review/{id}/resolve
- **Auth**: operator secret.
- **Request**: `{ resolvedByUserId?: uuid, resolutionNote: string }` (note required; user id optional).
- **Effect**: sets `Status=resolved`, `ResolvedAt=now`, records `ResolvedByUserId`/`ResolutionNote`. **Does not** itself write a ledger credit — resolution is a records action; any corrective credit is a separate, deliberate operator step documented in the runbook.
- **Response 200**: the updated item. **404** if not found; **409** if already resolved/dismissed; **401** (secret).

## POST /api/v1/payments/settlement-review/{id}/dismiss
- **Auth**: operator secret.
- **Request**: `{ resolvedByUserId?: uuid, resolutionNote: string }`.
- **Effect**: sets `Status=dismissed`, `ResolvedAt=now` (mismatch judged benign/expected).
- **Response 200**: the updated item. **404** / **409** / **401** as above.

## Non-goals
- No auto-credit of the expected amount (FR-B5 — block, don't guess).
- No Angular UI, no resident visibility.
- Resolution endpoints do not mutate the ledger; corrective financial action is manual and runbook-driven.

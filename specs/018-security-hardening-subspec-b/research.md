# Research: Sub-Spec B — Payment & Ledger Integrity

**Date**: 2026-07-18 · **Branch**: `018-security-hardening-subspec-b`
Refines the umbrella research/data-model (`specs/016-security-hardening/`) with the 2026-07-18 clarifications and a verified read of the current payments code. Where this document and the umbrella disagree, this document prevails (constitution §11 cross-spec consistency).

## Verified current state (2026-07-18 code map)

| Area | Fact (file:line) |
|------|------------------|
| ACH settlement | `Features/Payments/Webhooks/WebhookProcessor.cs::SettleSucceededAsync` (L73–89): flips `txn.Status = Succeeded` (L81) then `ledger.AddPaymentAsync(...)` (L82–83), then a single `db.SaveChangesAsync` (L88). The credit append opens its **own** transaction when called outside an ambient one. → two commits = double-credit window. |
| Card path (reference) | `Features/Payments/OneTime/ConfirmPaymentEndpoint.cs` (L89–108): `CreateExecutionStrategy()` → `BeginTransactionAsync` → add txn + ledger + receipt → `CommitAsync`. Already atomic. |
| Ledger append | `Features/Payments/Ledger/LedgerService.cs::AppendAsync` (L22–44): joins the ambient transaction if one exists (L27), else owns a retried one; `AppendCoreAsync` takes a per-property `pg_advisory_xact_lock` (L53) and computes monotonic `Sequence`. |
| Reconciliation | `Features/Payments/Jobs/ReconciliationService.cs::ResolvePendingAchAsync` (L33–62): for stuck pending ACH, calls the **same** `processor.SettleSucceededAsync(...)` (L49). |
| Webhook idempotency | `Features/Payments/Webhooks/StripeWebhookEndpoint.cs` (L60–79): dedupe by `WebhookEventInbox.StripeEventId` (unique), durable intake before processing. |
| Idempotency lookup | `Features/Payments/Services/IdempotencyService.cs::FindExistingAsync`: filters `PropertyId == propertyId && IdempotencyKey == key` (per-property). But the DB unique index on `IdempotencyKey` is **global** (`ApplicationDbContext.cs` ~L235; migration `20260606211720`). |
| Amount at settlement | `WebhookProcessor.cs::AttachSettlementAsync` (L179–187): fetches the charge, stores `ProcessorFeeAmount`/`BalanceTransactionId` — but never compares the provider's received amount to the server total. Server total is authoritative from PaymentIntent metadata (`ConfirmPaymentEndpoint.cs` L59–60). |
| LedgerEntry | `Domain/Entities/LedgerEntry.cs`: `PropertyId`, `Sequence` (unique `(PropertyId, Sequence)`), `TransactionId` (nullable, `SetNull`), `EntryType` (enum Payment/Charge/Refund/Chargeback/Reversal/ReturnedPaymentFee), `PaymentAmount`, `ChargeAmount`, `RunningBalance`. No unique on `TransactionId`. |
| Roles/authz | JWT claims are `sub`, `email`, `propertyId`, `communityId`, `jti` (`AuthService.cs` L155–159). **No role claim, no role model.** Privileged ops endpoints (`ReconcileEndpoint`) gate on the `X-Scheduler-Secret` shared secret. |
| Migration baseline | `20260607165538_OutboxDedupKey` (latest before this slice). |

## D-B1: Atomic settlement — single explicit transaction (mirror the card path)

- **Decision**: Wrap the ACH settlement body (`Status` flip + `AddPaymentAsync` + `EnsureReceiptAsync`) in one explicit transaction via `CreateExecutionStrategy()` → `BeginTransactionAsync` → `CommitAsync`, exactly as `ConfirmPaymentEndpoint` does. `AddPaymentAsync` already joins an ambient transaction (`LedgerService.cs` L27), so the credit lands inside the same boundary; the trailing `SaveChangesAsync` moves inside the transaction.
- **Rationale**: The card path is the proven atomic model (SC-B4 keeps it green). Reusing its shape minimizes novelty and keeps both success paths structurally identical.
- **Alternatives**: Rely only on the existing advisory lock (rejected — it serializes appends but does not make status+credit atomic across a crash); outbox/saga (rejected — over-engineered for a two-write unit already inside one DbContext).

## D-B2: Exactly-once backstop — the DB unique index is authoritative (clarified 2026-07-18)

- **Decision**: Add unique index `LedgerEntries (TransactionId, EntryType)`. A concurrent or redelivered second settlement's credit INSERT (EntryType=`Payment`, same `TransactionId`) hits the unique violation; the settlement path **catches `DbUpdateException` on that constraint and collapses to a no-op replay** (the first credit stands, status already `Succeeded`). Correctness rests on the DB constraint, so it holds across processes/Cloud Run instances — not on the in-process advisory lock or the `Status==Pending` guard (which remain as cheap fast-paths but are not the guarantee).
- **Rationale**: Clarification. Webhook redelivery and the reconciliation sweep run in different requests/instances; only a DB constraint is authoritative across them. `(TransactionId, EntryType)` (not `TransactionId` alone) preserves legitimate compensating entries — refund/reversal/chargeback carry the same `TransactionId` with a different `EntryType` (Assumptions; Edge Cases).
- **Since webhook and reconciliation share `SettleSucceededAsync`**, the fix + catch apply to both by construction — no second code path to patch.
- **Alternatives**: `TransactionId`-only unique (rejected — blocks compensating entries); app-level "does a Payment entry already exist?" check (rejected — TOCTOU race between check and insert; not authoritative).

## D-B3: Per-tenant idempotency — composite unique index (drop-and-recreate)

- **Decision**: Replace global unique `PaymentTransactions (IdempotencyKey)` with composite unique `(PropertyId, IdempotencyKey)`, matching the already-per-property `FindExistingAsync` lookup. On the create path, a unique violation is caught and collapsed to the existing transaction's replay response (return the original result, not a 500). The key is still forwarded verbatim to Stripe (`RequestOptions.IdempotencyKey`) — unchanged (Edge Cases).
- **Rationale**: Umbrella data-model §"Changed: PaymentTransactions". Aligns the DB constraint with the lookup semantics; removes the cross-tenant collision that surfaces as a server error.
- **Migration safety (§3, destructive)**: drop-and-recreate of a unique index. Pre-apply, query Dev for existing cross-tenant `IdempotencyKey` collisions (client-generated per checkout, so collisions are improbable but must be verified); include a rollback note restoring the global index.
- **Alternatives**: Keep global + catch-in-app (rejected — leaves the wrong constraint semantics and still 500s at the DB before the app can collapse it).

## D-B4: Amount cross-check — exact equality in integer minor units (clarified 2026-07-18)

- **Decision**: At settlement, convert the server-computed expected total to integer minor units (cents) and compare for **exact equality** with the provider-reported received amount (also minor units from Stripe). No tolerance/epsilon. A small pure helper (`Features/Payments/Settlement/SettlementAmounts.cs`) does the conversion+compare so it is unit-testable in isolation. Match → settle normally; mismatch → write **zero** ledger credits, leave the transaction un-settled/flagged, and insert a `SettlementReviewQueue` row (`ExpectedAmount`, `ProviderAmount`, `Currency`, `Status=open`).
- **Rationale**: Clarification. Stripe reports integer cents; comparing in minor units removes decimal-scale/float ambiguity and makes any mismatch a real discrepancy worth review. Exact (no epsilon) is the correct posture for a financial control.
- **Alternatives**: decimal equality (rejected — scale/representation ambiguity, mixes units); ±1-cent tolerance (rejected in clarification — knowingly admits a discrepancy).

## D-B5: Settlement review surface — backend endpoints + runbook, no UI (clarified 2026-07-18)

- **Decision**: New `SettlementReviewQueue` table (per umbrella data-model) + three FastEndpoints: `GET /payments/settlement-review` (list open, `limit`/`offset` paginated), `POST /payments/settlement-review/{id}/resolve` (record `ResolvedByUserId?`, `ResolutionNote`, set `Status=resolved`, `ResolvedAt`), `POST /payments/settlement-review/{id}/dismiss` (Status=`dismissed`). No Angular UI. A runbook (`runbooks/settlement-review-resolution.md`) documents operator resolution.
- **Rationale**: Clarification — rare ops-facing exception path; an authenticated, integration-tested API + runbook is in scope, a frontend is not (§12 — avoid unrelated surface area).
- **Alternatives**: full admin UI (rejected in clarification); table + SQL runbook only (rejected — no server-side authz/audit for resolution).

## D-B6: Review-endpoint authorization — shared-secret ops gate (FLAGGED — deviates from literal clarification wording)

- **Decision**: Gate the three review endpoints behind the existing **`X-Scheduler-Secret` operator shared-secret** pattern (`Jobs:SchedulerSharedSecret`, validated options — same mechanism as `ReconcileEndpoint`), **not** a role claim. `ResolvedByUserId` is optional (operator-supplied in the resolve body, else null with a `ResolutionNote`).
- **Rationale**: The clarification asked for "role-based authz," but the codebase has **no role model** — JWT carries no role claim and no endpoint enforces roles (verified). Building a role/permission system is unrelated product surface a payments-integrity slice must not carry (§12). The shared-secret gate is the established convention for privileged, non-resident operations (reconcile job), is server-side enforced (§7), keeps secrets externalized (§8), and matches the "ops-facing back-office" intent of the clarification. Letting any authenticated resident resolve settlement exceptions would be a conflict-of-interest/abuse vector, so plain "authenticated" is wrong; the secret gate is the correct in-scope choice.
- **Pre-existing gap noted**: the absence of a formal role model (constitution §7 names roles incl. "platform operator") is broader than this slice and is out-of-scope tech debt — a future authz-hardening effort, not payments integrity. **Surface to the user at plan report for confirmation before `/speckit.tasks`.**
- **Alternatives**: introduce an `admin`/`platform_operator` role + claim minting + policy (rejected — scope creep, touches auth/registration/seeding); authenticated-any (rejected — abuse vector).

## D-B7: FR-B6 defensive claim read — reuse Sub-Spec A

- **Decision**: Payment endpoints use A's merged `Features/Common/ClaimsPrincipalExtensions.RequirePropertyId()` (throws a mapped `DomainException` 401 when `propertyId` is absent) rather than null-forgiving claim reads. Since A (#100) is on `main`, this is a direct reuse.
- **Rationale**: FR-B6 is explicitly shared with A FR-A8, owned here for payment endpoints. No new mechanism.

## Cross-artifact supersessions

- Umbrella data-model already specifies the `LedgerEntries (TransactionId, EntryType)` unique index, the `PaymentTransactions (PropertyId, IdempotencyKey)` composite, and the `SettlementReviewQueue` shape — this feature's `data-model.md` refines those with the exact entity/enum names and the migration-safety runbook pointers. No contradictions introduced.

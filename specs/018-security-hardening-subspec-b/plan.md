# Implementation Plan: Sub-Spec B — Payment & Ledger Integrity

**Branch**: `018-security-hardening-subspec-b` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/018-security-hardening-subspec-b/spec.md`
**Umbrella**: `specs/016-security-hardening/` (shared data-model/research refined by this feature's local artifacts — local prevails where they differ; see research.md)

## Summary

Close the deferred-settlement double-credit window. Today `WebhookProcessor.SettleSucceededAsync` flips `PaymentTransaction.Status` and appends the ledger credit as **two separate commits** (the credit via `LedgerService.AddPaymentAsync`, which opens its own transaction when called outside an ambient one, then a second `SaveChangesAsync` for the status flip) — a crash between them, a webhook redelivery, or a race with the reconciliation sweep (`ReconciliationService.ResolvePendingAchAsync`, which calls the *same* `SettleSucceededAsync`) can produce a second credit. The fix wraps the status transition + ledger credit + receipt in **one explicit transaction** (mirroring the already-atomic card path in `ConfirmPaymentEndpoint`), backed by a **new `LedgerEntries (TransactionId, EntryType)` unique index** as the authoritative exactly-once guarantee: a concurrent/redelivered second credit INSERT hits the unique violation, caught and collapsed to a no-op replay. Two lower-severity items: make the `PaymentTransactions.IdempotencyKey` uniqueness **per-property** (matching the already-per-property replay lookup), and add a **settlement amount cross-check** (exact integer-minor-unit equality) that blocks mismatched credits into a new `SettlementReviewQueue` with backend-only resolution endpoints.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend only — no frontend in this slice)
**Primary Dependencies**: FastEndpoints, EF Core 9 (Npgsql), Stripe.net (existing gateway abstraction), FluentValidation (options), Serilog, OpenTelemetry (existing payment metrics)
**Storage**: PostgreSQL (Neon prod; Testcontainers CI/local). **New table** `SettlementReviewQueue`. **Index changes**: new unique `LedgerEntries (TransactionId, EntryType)`; replace global unique `PaymentTransactions (IdempotencyKey)` with composite unique `(PropertyId, IdempotencyKey)`. Migration baseline = `20260607165538_OutboxDedupKey`.
**Testing**: xUnit + Testcontainers.PostgreSQL, transaction-per-test isolation; business-process tests for settlement atomicity/idempotency/amount-check; concurrency test (two settlers, same payment); `Microsoft.AspNetCore.Mvc.Testing` for the review endpoints. Data-varied cases via Theories.
**Target Platform**: Google Cloud Run (migrations applied idempotently at startup)
**Project Type**: Web service (backend slice of the existing two-project app; `neko-hoa/` untouched)
**Performance Goals**: No settlement-throughput regression; the added unique index and single-transaction boundary are O(1) per settlement
**Constraints**: Forward-only (FR-B0) — no historical-duplicate repair or detection pass; the dup-index migration fails loud at boot if a historical duplicate exists (runbook resolves). Card path must stay atomic and green (SC-B4). Idempotency key must still pass through to Stripe unchanged (Edge Cases).
**Scale/Scope**: ~1 new table + 2 index migrations; refactor of one settlement method + its two callers; 1 new options-free service (amount check) folded into the processor; 3 review endpoints + runbook. No frontend, no new external services.

## Constitution Check

- **Technology fit**: PASS — .NET FastEndpoints REST, EF Core/PostgreSQL/Neon, Stripe.net, Serilog, OTel, Cloud Run, GitHub Actions. No new services; no frontend surface (justified — see below).
- **HOA tenancy**: PASS — `SettlementReviewQueue.PropertyId` is the tenant boundary (constitution §3); the idempotency change *strengthens* tenant isolation (per-property key uniqueness). Review endpoints scope queries and are not resident-accessible.
- **API contracts**: PASS — the 3 review endpoints document auth, the open-queue collection supports `limit`/`offset`, error shape is the existing `{code,message}`, responses are `no-store` (financial/authenticated). No breaking change to existing payment contracts.
- **Security and operations**: PASS — atomic multi-write settlement in a single transaction; uniqueness enforced at the persistence layer via strict migration; amount mismatches recorded without payment-instrument data; secrets externalized. **Authz note (flagged, see research D-B6)**: the review endpoints are gated by the existing **scheduler/operator shared-secret** pattern (as `ReconcileEndpoint`), not a role claim — the app has **no role model today** and introducing one is out of scope for a payments-integrity slice (§12 forbids unrelated surface area). This is server-side authorization consistent with the codebase's privileged-ops convention; the pre-existing absence of a formal role model is noted as out-of-scope tech debt.
- **File storage**: N/A — no blobs.
- **Caching/edge**: PASS — review responses `no-store`; no edge caching of authenticated data.
- **Testing discipline**: PASS — test-first; Testcontainers/PostgreSQL with transaction isolation; concurrency and crash-reprocess business-process tests; Theories for amount-boundary and cross-tenant idempotency cases.
- **CI/CD and documentation**: PASS — Sonar/Codecov + **90% changed-line diff-coverage gate** apply (write tests for every changed branch — the settlement refactor and catch-and-collapse path especially); Repowise markers refreshed on touched files.
- **Executable & living specs**: PASS — every FR maps to a named test (Test Map below); spec stays truthful; supersessions of umbrella data-model recorded in research.md.
- **Migration safety (§3)**: The idempotency index is a **drop-and-recreate** of a unique index — query for existing cross-tenant key collisions before applying, include a rollback note (destructive-migration rule). The `LedgerEntries` unique index is additive but fails loud on a historical duplicate (runbook). Both applied idempotently at Cloud Run startup.

**Gate result**: PASS — no unjustified violations. The no-frontend and secret-gated-authz decisions are justified above and in research.md; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/018-security-hardening-subspec-b/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D-B1..D-B7 (grounded in payments code)
├── data-model.md        # Phase 1 — SettlementReviewQueue + index changes (no card-path schema change)
├── quickstart.md        # Phase 1 — verify atomicity/idempotency/amount-check + the dup-index runbook
├── contracts/
│   └── settlement-review.md   # Phase 1 — the 3 review endpoints (list/resolve/dismiss)
├── runbooks/
│   ├── ledger-dup-index-failure.md   # boot-halt on historical duplicate (FR-B0/B2)
│   └── settlement-review-resolution.md # operator resolution of amount mismatches (FR-B5)
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/
├── Features/Payments/
│   ├── Webhooks/WebhookProcessor.cs          # SettleSucceededAsync → single explicit transaction; amount cross-check; catch+collapse unique violation
│   ├── Jobs/ReconcileEndpoint.cs             # unchanged call site (shares SettleSucceededAsync — covered by construction)
│   ├── Ledger/LedgerService.cs               # AddPaymentAsync joins ambient tx (already does); unique-violation surfaces to caller
│   ├── Services/IdempotencyService.cs        # unchanged lookup (already per-property); collision now a clean replay
│   ├── Settlement/SettlementAmounts.cs       # NEW: server-total → minor-units, exact-equality check helper
│   └── Settlement/                           # NEW: SettlementReviewQueue endpoints (List/Resolve/Dismiss) + models
├── Domain/Entities/
│   └── SettlementReviewItem.cs               # NEW entity (SettlementReviewQueue table)
└── Infrastructure/Persistence/
    ├── ApplicationDbContext.cs               # entity config + index changes
    └── Migrations/                           # NEW: SettlementReviewQueue table; LedgerEntries unique index; PaymentTransactions idempotency index drop+recreate

HOAManagementCompany.Tests/
├── Integration/Payments/
│   ├── SettlementAtomicityTests.cs           # NEW: crash-reprocess, redelivery, concurrency → exactly one credit (US1/SC-B1)
│   ├── IdempotencyTenantTests.cs             # NEW: cross-tenant key + same-tenant replay (US2/SC-B2)
│   ├── SettlementAmountCheckTests.cs         # NEW: minor-unit exact match/mismatch → credit vs review record (US3/SC-B3)
│   └── SettlementReviewEndpointTests.cs      # NEW: list/resolve/dismiss authz + behavior
└── Integration/Payments/ (existing card tests)# SC-B4 regression — card path stays atomic
```

**Structure Decision**: Existing backend layout; no new projects, no frontend. Changes concentrate in `Features/Payments/` (settlement processor + a small settlement subfolder for the amount helper and review endpoints) plus one entity, the DbContext config, and three migrations.

## Test Map (FR/SC → executable test)

| FR / SC | Test |
|---------|------|
| FR-B1, SC-B1 | `SettlementAtomicityTests` — status+credit+receipt commit together; interrupted-then-reprocessed writes no 2nd credit |
| FR-B2, FR-B3, SC-B1 | `SettlementAtomicityTests` — concurrent settlers + webhook redelivery → exactly one credit (unique-violation caught, collapsed); compensating entries (different EntryType) still allowed |
| FR-B4, SC-B2 | `IdempotencyTenantTests` — same key two properties both succeed; same-property replay returns original (no 500) |
| FR-B5, SC-B3 | `SettlementAmountCheckTests` — off-by-one-cent → zero credits + review record; exact match → settles |
| FR-B5 (surface) | `SettlementReviewEndpointTests` — list-open (paginated), resolve, dismiss; secret-gated authz; `no-store` |
| FR-B6 | Reuse A's `ClaimsPrincipalExtensions` defensive read on payment endpoints (integration assertion) |
| SC-B4 | Existing card-path settlement tests remain green |
| FR-B0/B2 migration | `quickstart.md` runbook step; migration applies cleanly on a fresh/seeded DB (no historical dup in test data) |

## Repowise Documentation

**Status**: In progress

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Features/Payments/Webhooks/WebhookProcessor.cs` | `domain=payments-settlement` | Atomic settlement + exactly-once backstop + amount cross-check |
| `HOAManagementCompany/Features/Payments/Settlement/*` (review endpoints) | `domain=settlement-review` | Amount-mismatch review queue + resolution |
| `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs` | `section=payments-integrity-indexes` | Ledger uniqueness + per-tenant idempotency |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise update --index-only`, marker validation |

## Risks & Coordination

1. **Historical duplicate at deploy** (FR-B0/B2): the `LedgerEntries` unique-index migration fails loud at Cloud Run startup if a duplicate settlement credit already exists in Dev/prod. Mitigation: runbook `ledger-dup-index-failure.md`; pre-deploy, query Dev for `(TransactionId, EntryType)` duplicates. **Ops touch-point on merge.**
2. **Idempotency index drop-and-recreate** (§3 destructive): query for existing cross-tenant `IdempotencyKey` collisions before applying (unlikely — keys are client-generated per checkout — but verify); rollback note in the migration.
3. **Authz mechanism deviates from the clarification's literal "role-based" wording** (research D-B6): resolved to the codebase's secret-gated ops pattern since no role model exists. Flagged for user confirmation before implement.
4. **Merge order**: independent of the other 016 slices; A (FR-A8 `ClaimsPrincipalExtensions`) is already merged, so FR-B6 reuses it directly.

## Complexity Tracking

No constitution violations requiring justification — table omitted.

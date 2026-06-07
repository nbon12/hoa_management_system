---
description: "PR Split 2 — backend reporting endpoints + remaining US1/US2 test gaps"
---

# Split 2 — Reporting endpoints + test/PCI gaps (backend)

**Story:** Cross-cutting backend (US1/US2 polish + Phase 6 reporting).
**Suggested branch:** `006-stripe-payments-reporting`
**Depends on:** PR #16, PR #19 (merged). Independent of Split 1 — can run in parallel.

## Goal

Close the remaining **backend** gaps that don't need the outbox or UI: statement/reporting endpoints,
drafts pagination, the variable-amount-notice + no-retry recurring test, the reconciliation/dead-letter
hardening test, and the SC-001 PCI-scope storage test. Small, mostly tests plus two read endpoints.

## Tasks

| Task | Description | Primary file(s) |
|------|-------------|-----------------|
| T081 | `GET /payments/statements` + `GET /payments/unpaid-assessments` (FR-039, NC § 47F-3-118) **with Testcontainers tests** | `Features/Payments/Statements/` |
| T065 | Drafts query: surface status from linked `PaymentTransaction`; add `limit`/`offset` pagination (constitution §4) | `Features/Payments/DraftsEndpoint.cs` |
| T056 | Testcontainers test: variable-amount advance notice enqueued before draft (FR-011c); disable→no drafts; **failed draft NOT auto-retried within cycle, waits for next draft day (FR-011a)** | `RecurringNoticeTests.cs` |
| T083 | Reconciliation + dead-letter hardening test (missed-webhook backfill, outbox flush, inbox retry) | `ReconciliationTests.cs` |
| T034 | SC-001 test: confirm **no raw card/bank number is accepted or stored** — inspect the persisted schema + a charged transaction | `PciScopeTests.cs` |

## Notes / gotchas

- **T056 variable-amount notice:** the notice channel rides the outbox/alert plumbing built in Split 1.
  If Split 1 has not merged, scope T056 here to the **"no auto-retry within cycle"** assertion (which
  only needs the existing run-drafts idempotency) and defer the *advance-notice-enqueued* assertion to a
  follow-up, or sequence this PR after Split 1. Call this out in the PR description.
- **T065 pagination:** keep the existing 12-month default window; add `limit`/`offset` on top so the
  current `GetDraftsAsync` contract stays backward compatible.
- **T034** is a verification test, not new production code — it asserts the vaulted model already holds
  (only `pm_…` references + masked display detail persist).

## Definition of done

- Statements + unpaid-assessments endpoints return correct figures with tests.
- Drafts endpoint paginates and reflects linked-transaction status.
- Recurring no-retry semantics proven; reconciliation/dead-letter paths proven.
- PCI-scope test passes against the live schema; ≥90% diff coverage; CI green.

## Out of scope

The statement **UI** (T082 → Split 4). Anything requiring the alert providers (Split 1).

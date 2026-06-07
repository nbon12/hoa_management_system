---
description: "PR split plan for the remaining 006-stripe-payments work"
---

# 006-stripe-payments — remaining work, split into PRs

This folder breaks the **still-open** tasks from [`../tasks.md`](../tasks.md) into reviewable
PRs. It was authored after PRs #16 (US1 one-time backend), #19 (US2 recurring backend), and the
tasks.md staleness audit — so it reflects what is *actually* left, not the raw unchecked count.

## What is already done (do NOT re-open)

- **US1 backend (PR #16):** one-time intent/confirm, `FeeCalculator`, `LedgerService`/`AllocationService`,
  `IdempotencyService`, durable webhook intake **and all lifecycle handlers** (success/fail, refunds,
  disputes, ACH-return) consolidated in `WebhookProcessor.cs`, reconciliation ACH-pending sweep,
  webhook tests. (T018–T023, T032–T034*, T039–T049, T042–T046)
- **US2 backend (PR #19):** SetupIntent, vaulted-PM recurring upsert + immutable mandate, GET/DELETE
  recurring, off-session run-drafts sweep, vaulted-method migration (drops masked columns), tests.
  (T020, T053–T055, T060–T064)

> Note the divergence from the planned file layout: webhook handlers live in `WebhookProcessor.cs`,
> not `Webhooks/Handlers/*.cs`. The **only** still-pending piece of US1 webhooks is the alert-hook
> enqueue (T045 tail), which is delivered in **Split 1** with the rest of US3.

## The split (recommended order)

| # | PR | Track | Tasks | Depends on |
|---|----|-------|-------|------------|
| 1 | [pr1-alerts-backend](pr1-alerts-backend.md) | backend | T024, T026, T069–T071, T074–T079, T029 | #16, #19 (merged) |
| 2 | [pr2-reporting-and-test-gaps](pr2-reporting-and-test-gaps.md) | backend | T034, T056, T065, T081, T083 | #16, #19 (merged) — parallel to #1 |
| 3 | [pr3-onetime-ui](pr3-onetime-ui.md) | frontend | T028, T050, T051, T035–T038 (+ FE CI job) | #16 (merged) |
| 4 | [pr4-recurring-alerts-ui](pr4-recurring-alerts-ui.md) | frontend | T066, T067, T057–T059, T080, T072, T073, T082 | Split 1, Split 2, Split 3 |
| 5 | [pr5-compliance-and-gates](pr5-compliance-and-gates.md) | closeout | T052, T068, T084–T093 | all of the above |

Splits 1 and 2 are independent and can be worked in parallel. Split 3 is independent of 1/2 and can
start any time. Split 4 needs the alert-preferences endpoints (1), the statements endpoint (2), and the
Stripe.js/CI scaffolding (3). Split 5 is the final hardening pass.

## Conventions (unchanged from prior PRs)

- Backend integration tests: PostgreSQL/Testcontainers; Stripe/Twilio/SendGrid mocked via
  `IStripeGateway` / `IAlertProvider` — no real external calls.
- CI gate: `dotnet test` (Release) + `diff-cover` **≥90%** on changed C# lines vs `origin/main`.
- Each PR branches off latest `main`; commit trailer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

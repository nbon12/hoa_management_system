---
description: "PR Split 5 — endpoint verification, compliance, security, accessibility, and release gates"
---

# Split 5 — Compliance, security, accessibility & release gates (closeout)

**Story:** Phase 6 polish + cross-cutting verification.
**Suggested branch:** `006-stripe-payments-polish`
**Depends on:** Splits 1–4 (verification needs the alert, reporting, and UI surfaces to exist).

## Goal

Final hardening and evidence pass before declaring 006 done: prove the endpoints are validated,
audited, and PII-free in telemetry; document the compliance/ops posture (PII-at-rest, rate limits, NC
fee caps, backups/PITR); verify SC-008; refresh Repowise markers; and confirm the CI/coverage gates and
the quickstart end-to-end.

## Tasks

| Task | Description |
|------|-------------|
| T052 | US1: validation/error-shape/audit logging on intent/confirm; verify dev Swagger + PII-free Sentry/OTel spans |
| T068 | US2: validation/error-shape/audit logging on recurring + job endpoints; verify Swagger + PII-free telemetry |
| T084 | PII encryption-at-rest review (FR-029) + audit logging of financial-record access and fee/alert/schedule config changes |
| T085 | Rate-limit review on intent/confirm/setup/jobs endpoints + Stripe Radar fraud-tooling note (FR-028) |
| T086 | Document NC late-fee/interest caps config seed + surcharge-jurisdiction gating; confirm `SurchargingEnabled` defaults safe |
| T087 | Document backups/PITR + RPO/RTO + Stripe-based reconstruction (FR-036) in `quickstart.md` ops section |
| T088 | Accessibility pass (WCAG 2.1 AA) for payment, auto-pay, alerts, and statement surfaces |
| T089 | Verify SC-008: confirm 0 deprecated masked card/bank columns remain post-migration; migration rollback/mitigation review |
| T090 | Update Repowise marker regions for `PaymentService.cs`, `Program.cs`, webhook + jobs files |
| T091 | Verify Sonar PR scan, Codecov ≥95% changed-file coverage, and the 90% diff-coverage gate pass |
| T092 | Run `quickstart.md` end-to-end (Stripe CLI webhooks, draft + reconcile job curls) |
| T093 | Playwright browser test for the Stripe Payment Element interaction (in-iframe field entry, constitution §9) — `neko-hoa/tests/playwright/payment-element.spec.ts` |

## Notes

- T052/T068 are largely *verification + add-audit-logging* over endpoints that already exist; expect
  small diffs (logging, a few validators) rather than new features.
- T089 can lean on the migration already authored in PR #19 (`RecurringVaultedMethod` drops all 7 masked
  columns) — this task is the explicit *confirmation + rollback review*, the receipt for SC-008.
- T093 covers what Cypress can't: typing into the Stripe iframe fields. Keep it out of the Cypress suite.
- This PR is the right place to flip any remaining `[ ]` in `../tasks.md` to `[X]` and write the final
  completion summary.

## Definition of done

- Endpoints validated/audited; telemetry verified PII-free; Swagger renders in dev.
- Compliance/ops docs landed in `quickstart.md`; SC-008 confirmed (0 masked columns).
- a11y pass complete; Playwright Element test green; all CI gates pass.
- `tasks.md` fully reconciled and the feature declared complete.

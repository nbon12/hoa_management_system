---
description: "PR Split 4 — auto-pay setup UI, failure-alert opt-in UI, and statement view (US2+US3 frontend)"
---

# Split 4 — Auto-pay + alerts + statement UI (US2 & US3 frontend)

**Story:** US2 (auto-pay) and US3 (alert opt-in) frontend, plus the statement view.
**Suggested branch:** `006-stripe-payments-recurring-alerts-ui`
**Depends on:** Split 3 (Stripe.js + FE CI + service patterns), Split 1 (alert-preferences endpoints),
Split 2 (statements endpoint for the statement view).

## Goal

Build the resident-facing auto-pay screen on top of the SetupIntent/mandate backend: choose amount type
(fixed/assessment/balance), draft day, accept the mandate, and see a status card + drafts table. On the
same screen, add the **"Payment alerts" opt-in** section (SMS/email, default OFF, phone required for
SMS). Separately, build the read-only **statement/transactions view**.

> Like Split 3, the recurring component today is a **raw-card mock** (`cardNumber`/`routing`/`accountNum`
> placeholders) — replace it with a SetupIntent Element, do not extend it.

## Tasks

| Task | Description | Primary file(s) |
|------|-------------|-----------------|
| T067 | Add setup-intent/recurring methods to the payments service | `core/services/payments.service.ts` |
| T066 | Rebuild auto-pay page (SetupIntent element, amount type, draft day, mandate checkbox, status card, drafts table) | `features/payments/recurring/recurring.component.ts` |
| T080 | "Payment alerts" opt-in section + service methods (get/put alert-preferences) | `features/payments/recurring/alerts/alerts.component.ts`, `payments.service.ts` |
| T082 | Angular statement/transactions view (consumes statements/unpaid-assessments from Split 2) | `features/payments/statement/statement.component.ts` |
| T057 | Angular Testing Library test for the auto-pay component | `recurring.component.spec.ts` |
| T058 | Cypress E2E for auto-pay setup | `cypress/e2e/recurring-setup.cy.ts` |
| T059 | Storybook story for the status card + drafts table | `recurring.stories.ts` |
| T072 | Angular Testing Library test for the alerts opt-in section | `alerts/alerts.component.spec.ts` |
| T073 | Cypress E2E for alert opt-in/opt-out | `cypress/e2e/alert-preferences.cy.ts` |

## Notes

- The alerts section lives **inside** the auto-pay page (`recurring/alerts/`), which is why US2 and US3
  frontend ship together — they're one screen for the resident.
- SMS opt-in must require a phone number and surface TCPA/STOP copy consistent with the Twilio provider
  (Split 1, T076).
- The status card's next-draft amount/date and masked method come straight from
  `GET /payments/recurring` — don't recompute the fee in Angular.
- T082 (statement view) is grouped here to keep frontend to two PRs; if Split 2 hasn't merged, hold T082
  back or stub the endpoint.

## Definition of done

- Resident can enroll/cancel auto-pay via SetupIntent + mandate; status card + drafts table render.
- Alert opt-in/opt-out persists and round-trips; SMS requires a phone.
- Statement view renders ledger/transactions + unpaid assessments.
- Component + service tests pass; Cypress flows pass; FE CI green.

## Out of scope

Backend alert/reporting logic (Splits 1/2). Accessibility pass + Playwright iframe test (Split 5).

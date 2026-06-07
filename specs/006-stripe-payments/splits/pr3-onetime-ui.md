---
description: "PR Split 3 — one-time payment UI on the Stripe Payment Element (US1 frontend)"
---

# Split 3 — One-time payment UI (US1 frontend)

**Story:** US1 — *Pay an assessment one time, securely* (Priority P1), frontend.
**Suggested branch:** `006-stripe-payments-onetime-ui`
**Depends on:** PR #16 (one-time backend + webhooks) — merged. Independent of Splits 1/2.

## Goal

Replace the **legacy raw-card mock** one-time payment UI (which today collects PAN/CVV/routing in plain
`ngModel` inputs against the mock-data service) with a real **Stripe Payment Element** flow: fetch
options → render the Element → confirm via the backend `intent`/`confirm` endpoints. No card or bank
number ever touches Angular state or our API (SC-001).

> ⚠️ This is a *replacement*, not an extension. `one-time.component.ts` currently has
> `cardNumber`/`cardCvc`/`routing`/`accountNum` fields — those must be deleted, not reused.

This is the first frontend PR in the feature, so it also stands up Stripe.js and a **frontend test job
in CI** (today CI runs backend `dotnet test` only; the new Jasmine/Cypress specs need a runner or they
are dead weight).

## Tasks

| Task | Description | Primary file(s) |
|------|-------------|-----------------|
| T028 | Initialize Stripe.js (`loadStripe`), expose `stripe$`; register `provideNgxStripe()` | `core/services/payments.service.ts`, `app.config.ts` |
| T051 | Add options/intent/confirm/transactions/receipt methods to the payments service | `core/services/payments.service.ts` |
| T050 | Rebuild one-time component with Stripe Payment Element, presets, masked summary (Amount/Fee/Total), confirm flow | `features/payments/one-time/one-time.component.ts` |
| T035 | Angular Testing Library component test | `one-time.component.spec.ts` |
| T036 | Jasmine unit test for the service (options/intent/confirm) | `payments.service.spec.ts` |
| T037 | Cypress E2E for one-time payment | `cypress/e2e/one-time-payment.cy.ts` |
| T038 | Storybook story + visual case | `one-time.stories.ts` |
| — | **CI:** add a frontend job (`npm ci && npm run lint && npm test -- --watch=false`) so component/service specs actually run | `.github/workflows/*.yml` |

## Notes

- Use `@stripe/stripe-js` + `ngx-stripe` (the latter is already referenced by T028). Pin a publishable
  key from the backend `setup-intent`/`options` response — never hardcode.
- The Payment Element runs in a Stripe iframe, so Cypress (T037) drives the surrounding app and asserts
  the confirm→receipt flow against a stubbed/test backend; the **in-iframe field entry** is covered
  separately by Playwright in Split 5 (T093).
- Fee display must mirror the backend `FeeCalculator` output (flat convenience fee vs % credit
  surcharge), not recompute client-side.

## Definition of done

- Raw-card inputs gone; one-time payment completes via Payment Element against the real backend.
- Frontend CI job runs and is green; component + service unit tests pass; Cypress happy-path passes.

## Out of scope

Auto-pay UI, alerts UI, statement view (Split 4). Playwright iframe test (Split 5/T093).

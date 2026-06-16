# Feature Specification: Playwright Post-Deploy Smoke Suite for Dev

**Feature Branch**: `012-playwright-dev-smoke`
**Created**: 2026-06-16
**Status**: Implemented

## Overview

After each successful deploy of the dev environment (Cloud Run candidate + Cloudflare Pages preview), run the existing Playwright e2e suite against the live URLs before promoting the candidate to 100% traffic. This catches integration failures — broken auth, real API wiring, Stripe Payment Element rendering — that the hermetic Cypress suite (which stubs the backend and Stripe.js) cannot surface.

## Background

The dev deployment pipeline (`deploy-dev` job in `test.yml`) already had two gates before promotion:

1. **Backend health gate** — polls `/health` until the Cloud Run candidate is ready (handles DB migration delay)
2. **Cypress e2e gate** (`e2e:dev`) — runs the hermetic Cypress suite pointed at the Cloudflare Pages preview

The Cypress suite uses `cy.intercept` to stub all backend calls and Stripe.js, so it validates UI logic but cannot catch real-stack failures (wrong API base URL in the deployed build, broken auth token flow, Stripe iframe not loading, etc.).

The Playwright suite (`neko-hoa/e2e/`) was already written to cover these real-stack paths — auth flow with a real backend, payment statements, one-time and recurring payment UI with the real Stripe Payment Element — but was only run locally against `localhost`. This feature wires it into the dev deploy gate.

## What Was Built

### Changes

| File | Change |
|------|--------|
| `neko-hoa/playwright.config.ts` | `baseURL` reads from `PLAYWRIGHT_BASE_URL` env var; falls back to `http://localhost:4200` |
| `neko-hoa/e2e/global-setup.ts` | API cleanup URL and browser context base URL read from `PLAYWRIGHT_API_URL` / `PLAYWRIGHT_BASE_URL` env vars |
| `neko-hoa/package.json` | Added `e2e:playwright-dev` script (`playwright test`) |
| `.github/workflows/test.yml` | Added Playwright step in `deploy-dev` job between Cypress gate and promote steps |

### Dev Deploy Gate Order (after this change)

1. Backend health gate (Cloud Run candidate `/health`)
2. Frontend build + Cloudflare Pages preview deploy
3. **Cypress gate** — hermetic, fast, catches UI regressions
4. **Playwright gate** — real stack, catches auth/API/iframe integration failures
5. Promote backend candidate to 100% traffic
6. Promote frontend preview to main

Steps 3 and 4 must both pass before any promotion happens. A failure at either step leaves the prior healthy release serving 100% of traffic.

### Environment Variables (injected by the workflow)

| Variable | Value | Purpose |
|----------|-------|---------|
| `PLAYWRIGHT_BASE_URL` | Cloudflare Pages preview URL | Frontend base for Playwright and global-setup browser context |
| `PLAYWRIGHT_API_URL` | Cloud Run candidate URL | API base for the E2E cleanup call in global-setup |

Both fall back to `localhost` defaults so local `npm run e2e` continues to work unchanged.

## Scope

### In scope
- Wiring the existing Playwright suite into the dev deploy gate
- Making Playwright's URLs configurable via env vars

### Out of scope
- Writing new Playwright tests (the existing suite covers auth, payments, community, property, dashboard)
- Running Playwright on PRs / feature branches (dev deploy only runs on merge to main)
- Running Playwright against staging or production environments

## Test Coverage

The Playwright suite (`neko-hoa/e2e/`) covers:

- **`auth.spec.ts`** — portal navigation, login, auth guard redirect, registration
- **`payments.spec.ts`** — payment statement, one-time payment flow, recurring payment CRUD
- **`payment-element.spec.ts`** — Stripe Payment Element iframe loading and interaction
- **`community.spec.ts`** — community listing and detail views
- **`property.spec.ts`** — property management flows
- **`dashboard.spec.ts`** — dashboard rendering and summary data

All tests run under a pre-authenticated session (seeded via `global-setup.ts` against `resident@nekohoa.dev`) so test-to-test isolation is handled by the auth state file.

## Operational Notes

- The dev backend must have the seed user (`resident@nekohoa.dev`) present and the `DELETE /api/v1/e2e/cleanup` endpoint available — both are already true for the dev environment.
- Playwright installs Chromium at CI run time (`npx playwright install --with-deps chromium`); no persistent browser cache is required.
- Failure notification (Slack webhook) is already wired at the job level — a Playwright failure triggers the same alert as any other `deploy-dev` failure.

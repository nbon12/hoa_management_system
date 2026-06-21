# Contract: Post-deploy smoke gate (US2)

## Selection contract

- Curated deployment-health checks are tagged `@smoke` (Playwright `{ tag: '@smoke' }`), mirroring the existing `@local-only` convention.
- The post-deploy gate runs **only** `@smoke`: `npm run e2e:playwright-smoke` → `playwright test --grep @smoke`.
- The full regression suite remains runnable unchanged via `npm run e2e` (and `e2e:dev` Cypress) for local/PR use.
- CI (`.github/workflows/test.yml`, "Playwright smoke suite" step) invokes `e2e:playwright-smoke` instead of `e2e:playwright-dev`.

## Smoke set membership rules

A check qualifies for `@smoke` only if it is **read-only** and deployment-health-oriented:

| Included (read-only) | Excluded (kept in full suite only) |
|----------------------|-------------------------------------|
| Login/portal page renders (anon) | Registration (claims a property) |
| Authenticated dashboard/app shell renders | Auto-pay toggle (durably disables seed enrollment) |
| Key authenticated pages load without error | Poll vote / RSVP (write data) |
| API reachable / health responds | Payment submission |
| | `@local-only` Stripe-iframe specs |

## Required behaviors (map to acceptance scenarios)

| # | Given | When | Then | Scenario |
|---|-------|------|------|----------|
| SM-1 | The smoke gate | runs | executes only `@smoke` checks, not the full suite | US2 #1; SC-004 |
| SM-2 | A shared environment | smoke gate completes | no created accounts, no toggled enrollment, no reliance on `/e2e/cleanup` | US2 #2; SC-005 |
| SM-3 | Repeated runs | environment data varies within bounds | deterministic pass/fail (no data-dependency flakes) | US2 #3 |
| SM-4 | Developers | run `npm run e2e` locally/PR | full regression suite still available | US2 #4; FR-007 |
| SM-5 | A genuine deployment break (auth down, page not rendering, API unreachable) | smoke gate runs | fails loudly | US2 #5; edge case |

## Determinism / side-effect rules

- Zero state-mutating tests in the gate (SC-004).
- Running N times leaves the data the gate owns equivalent before/after (SC-005). `global-setup` login (token issuance only) and its best-effort, non-fatal `DELETE /e2e/cleanup` are permitted because the smoke set neither registers users nor depends on cleanup correctness (FR-006).

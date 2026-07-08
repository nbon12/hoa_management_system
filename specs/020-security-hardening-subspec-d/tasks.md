# Tasks: Sub-Spec D — Frontend Session & Content Security

**Input**: Design documents from `/specs/020-security-hardening-subspec-d/`
**Prerequisites**: plan.md, research.md (D-R1…D-R7), data-model.md, contracts/auth-session.md, quickstart.md
**Tests**: Included — the constitution mandates test-first (red→green) for every acceptance criterion.

**Organization**: Tasks grouped by user story (US1 = P1 cookie/CSP containment, US2 = P2 credential removal + interceptor scoping, US3 = P3 minor hardening).

## Phase 1: Setup

- [ ] T001 Reconcile branch with 017-A: merge latest `main` (or `017-security-hardening-subspec-a` if #100 unmerged) into `020-security-hardening-subspec-d`; resolve expected small conflicts in `HOAManagementCompany/Program.cs` and `HOAManagementCompany/Features/Auth/AuthService.cs` (A adds lockout/DI; D changes login/refresh transport). Backend endpoint tasks below assume A's code is present.

## Phase 2: Foundational (blocking all stories)

- [ ] T002 Add `RefreshCookieOptions` (`Auth:RefreshCookie: { SameSite }`) + FluentValidation validator (`SameSite ∈ {Strict,Lax,None}`) in `HOAManagementCompany/Infrastructure/Configuration/RefreshCookieOptions.cs` and `RefreshCookieOptionsValidator.cs`; register via `AddValidatedOptions` in `HOAManagementCompany/Program.cs`; set per-env values (`appsettings.json` Production=Strict, `appsettings.Dev.json`=None, `appsettings.Development.json`=Lax, `appsettings.Test.json`=Lax) per contracts/auth-session.md environment matrix. Include a startup-validation Theory case in `HOAManagementCompany.Tests/Integration/Configuration/StartupValidationTests.cs` (invalid SameSite value aborts startup).

## Phase 3: User Story 1 — Contain credential theft from script compromise (P1) 🎯 MVP

**Goal**: Refresh token in HttpOnly cookie (config-driven SameSite), access token in memory, hint-gated silent refresh on startup, enforcing per-build CSP.

**Independent test**: After login, refresh token not script-readable; reload keeps session with exactly one refresh call; deployed response serves the enforcing CSP; payments still work.

### Tests (write first — must fail)

- [ ] T003 [P] [US1] Backend integration test `HOAManagementCompany.Tests/Integration/Security/AuthCookieTests.cs`: login sets `neko_refresh` cookie (HttpOnly, Secure, `Path=/api/v1/auth`, `Max-Age=2592000`, SameSite from config — Theory over Strict/Lax/None) and body omits `refreshToken`; refresh with cookie rotates + re-sets; refresh without cookie or with bad Origin → 401 generic + cookie cleared; logout revokes + clears. Transaction-isolated per constitution.
- [ ] T004 [P] [US1] Karma spec `neko-hoa/src/app/core/services/token.service.spec.ts`: access token held in memory only; no `neko_token`/`neko_refresh`/`neko_user` localStorage writes; legacy keys wiped on init; `neko_has_session` hint set/cleared correctly.
- [ ] T005 [P] [US1] Karma spec `neko-hoa/src/app/core/services/session-refresh.spec.ts`: startup initializer calls `/auth/refresh` (withCredentials) only when hint present; success stores token + re-sets hint; 401 clears hint and stays anonymous; no call when hint absent.
- [ ] T006 [P] [US1] Cypress e2e `neko-hoa/cypress/e2e/session-security.cy.ts`: login → reload → session survives via silent refresh; `localStorage`/`document.cookie` expose no token material; logout → reload → no refresh request fired.
- [ ] T007 [P] [US1] Headers assertion: node test (run in `npm run test:ci` or a build-check script step) that `dist/neko-hoa/browser/_headers` exists after `stamp-headers.mjs`, contains no `__API_ORIGIN__`, and includes `js.stripe.com` (script/frame), `api.stripe.com` (connect), and the stamped API origin — `neko-hoa/scripts/stamp-headers.test.mjs`.

### Implementation

- [ ] T008 [US1] Backend: set/clear the refresh cookie per contract in `HOAManagementCompany/Features/Auth/LoginEndpoint.cs`, `RefreshEndpoint.cs` (read cookie not body; verify `Origin`/`Referer` against CORS allowlist/suffixes; rotate + re-set; clear on 401), `LogoutEndpoint.cs` (clear); drop `refreshToken` from `AuthResponse` in `AuthService.cs`; mark responses `Cache-Control: no-store`.
- [ ] T009 [US1] `neko-hoa/src/app/core/services/token.service.ts`: in-memory access-token signal; remove all localStorage token/user persistence; one-time legacy-key wipe; manage `neko_has_session` hint.
- [ ] T010 [US1] `neko-hoa/src/app/core/services/auth.service.ts`: login/logout use cookie flows (`withCredentials` on `/api/v1/auth/*`), populate in-memory user from responses; drop storage restoration.
- [ ] T011 [US1] NEW `neko-hoa/src/app/core/services/session-refresh.ts`: refresh coordinator (shared observable; Web Locks `neko-refresh` + `BroadcastChannel('neko-auth')` per research D-R3, graceful per-tab fallback) + hint-gated `APP_INITIALIZER` factory; register in `neko-hoa/src/app/app.config.ts` after the config guard.
- [ ] T012 [P] [US1] NEW `neko-hoa/src/assets/_headers` (enforcing CSP + baseline headers per research D-R4, `__API_ORIGIN__` placeholder) and NEW `neko-hoa/scripts/stamp-headers.mjs` (stamps origin into `dist/neko-hoa/browser/_headers`, exits non-zero if placeholder remains); verify the `src/assets` rule in `neko-hoa/angular.json` copies it.
- [ ] T013 [US1] CI: run `stamp-headers.mjs` with the deployment's API origin in `.github/workflows/test.yml` (deploy-dev job, before Pages deploy — Dev API origin) and `.github/workflows/pr-env.yml` (step 7, alongside the existing environment sed — the PR's API origin). Add a `@smoke`-tagged Playwright assertion (`neko-hoa/e2e/` smoke spec) that the served frontend response carries the enforcing `Content-Security-Policy` header with the stamped API origin — executable backing for SC-D2 / US1 acceptance scenario 2 (analysis C1).
- [ ] T014 [US1] Update `specs/016-security-hardening/contracts/auth-session.md` cookie note + T096 to point at this feature's contract, and mark the umbrella `tasks.md` US5 phase (T061–T097) as superseded by `specs/020-security-hardening-subspec-d/tasks.md` (supersession per constitution §11 corpus consistency; analysis I1/I2).

**Checkpoint**: US1 independently deliverable — cookie transport + CSP live, all US1 tests green.

## Phase 4: User Story 2 — Remove committed credentials and scope token transmission (P2)

**Goal**: No tracked real token; bearer attached only to API-origin requests; concurrent 401s produce exactly one refresh.

**Independent test**: `git ls-files` shows no auth-state file; interceptor specs prove origin scoping + single-flight; e2e still passes.

### Tests (write first — must fail)

- [ ] T015 [P] [US2] Karma spec `neko-hoa/src/app/core/interceptors/auth.interceptor.spec.ts`: bearer attached only when request URL starts with `environment.apiBaseUrl` (non-API origin gets none); concurrent 401s share one refresh (single-flight via session-refresh coordinator); refresh failure logs out once.

### Implementation

- [ ] T016 [US2] `neko-hoa/src/app/core/interceptors/auth.interceptor.ts`: origin-scope bearer attachment; route 401 retry through the `session-refresh` coordinator (in-tab shared observable + cross-tab lock from T011); remove body-based refresh call.
- [ ] T017 [P] [US2] `git rm --cached neko-hoa/e2e/.auth/state.json`; add `e2e/.auth/` to `neko-hoa/.gitignore`; confirm `neko-hoa/e2e/global-setup.ts` regeneration keeps Playwright green. Add an executable scan assertion backing SC-D3 (analysis C2): a check (unit/e2e or CI step alongside T007's headers check) that `git ls-files neko-hoa/e2e/.auth/` returns nothing.
- [ ] T018 [US2] Ops (human/one-time, after merge to Dev): revoke the exposed seed-user refresh tokens in the Dev DB per the SQL in `quickstart.md`; record completion in this file.

**Checkpoint**: US2 independently deliverable.

## Phase 5: User Story 3 — Minor content and navigation hardening (P3)

**Goal**: Safe opener ordering, base64url-tolerant expiry parsing, starter cruft + dead controls removed.

**Independent test**: Component/unit specs for opener order and base64url; removed content absent from bundle.

### Tests (write first — must fail)

- [ ] T019 [P] [US3] Extend `neko-hoa/src/app/core/services/token.service.spec.ts` with a base64url Theory-style case set: `isTokenExpired` decodes payloads containing `-`/`_` (and missing padding) correctly — valid future-dated token not reported expired.
- [ ] T020 [P] [US3] Component spec `neko-hoa/src/app/features/community/documents/documents.component.spec.ts`: opener nulled **before** navigation assignment on the pre-opened tab path.

### Implementation

- [ ] T021 [US3] `token.service.ts`: base64url-normalize (`-`→`+`, `_`→`/`, pad) before `atob` in `isTokenExpired`.
- [ ] T022 [P] [US3] `neko-hoa/src/app/features/community/documents/documents.component.ts`: set `tab.opener = null` before `tab.location.href = url`.
- [ ] T023 [P] [US3] Remove Angular starter-template markup from `neko-hoa/src/app/app.component.html` (keep `<router-outlet/>`) and the non-functional "Continue with Google" button from the login component (template + any handler); update affected component specs. Then sweep for remaining unsafe outbound links (analysis A1): `grep -rn 'target="_blank"' neko-hoa/src --include='*.html'` — every hit must carry `rel="noopener noreferrer"` (fix any that don't; FR-D6).

**Checkpoint**: US3 independently deliverable.

## Phase 6: Polish & Cross-Cutting

- [ ] T024 [P] Refresh Repowise marker regions for changed auth/session files (`RefreshEndpoint.cs`, `auth.interceptor.ts`, CSP doc) per plan.md Repowise table.
- [ ] T025 Full verification: `dotnet test` (Category!=Sandbox), `npm run test:ci`, `npm run build` + stamped-headers check, `npm run e2e:ci`; confirm deployed smoke expectations unchanged. Explicitly confirm the existing boot-time config-guard specs stay green — `neko-hoa/src/app/core/config/runtime-config.validator.spec.ts` (FR-D8 preservation; analysis C3).
- [ ] T026 Pre-PR freshness gate (constitution §11): update this feature's `spec.md` + `tasks.md` to match work actually performed; verify no older spec.md drifted (016 umbrella contract supersession noted in T014).

## Dependencies & Execution Order

- **Phase 1 → 2 → 3**: T001 (branch sync) before backend tasks; T002 before T003/T008.
- **US1 → US2**: T016 consumes the T011 coordinator and T009 in-memory token service. US2's T017 is independent (can run any time).
- **US3**: logically independent; sequence T019/T021 after US1's T009 lands (same file `token.service.ts`) to avoid conflicts; T020/T022/T023 fully parallel.
- **Test-first**: within each story, test tasks precede implementation tasks (red→green).

```text
T001 → T002 → { T003..T007 (P) } → T008..T014 → US1 ✓
                                   ↘ T015 → T016 → T017(P), T018(ops) → US2 ✓
                                   ↘ T019/T020 (P) → T021..T023 → US3 ✓
                                                     → T024..T026 (polish)
```

## Parallel Examples

- After T002: T003, T004, T005, T006, T007 all parallel (different files).
- Within US1 impl: T012 ∥ T009/T010; T013 after T012.
- US3: T020, T022, T023 parallel; T019/T021 serialized behind token.service changes.

## Implementation Strategy

**MVP = Phase 3 (US1)**: cookie transport + CSP is the risk-retiring core and is independently shippable (interceptor still attaches bearer broadly until US2 — unchanged current behavior, not a regression). Then US2 (small), US3 (trivial), Polish. Single PR (#103 lineage) ships all phases atomically with the backend contract change; A+D merge coordination and the unspecced signup-UI gap are tracked in plan.md Risks.

## Task Count Summary

| Phase | Tasks |
|-------|-------|
| Setup | 1 |
| Foundational | 1 |
| US1 (P1) | 12 (5 tests, 7 impl) |
| US2 (P2) | 4 (1 test, 3 impl/ops) |
| US3 (P3) | 5 (2 tests, 3 impl) |
| Polish | 3 |
| **Total** | **26** |

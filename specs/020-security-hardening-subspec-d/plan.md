# Implementation Plan: Sub-Spec D — Frontend Session & Content Security

**Branch**: `020-security-hardening-subspec-d` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/020-security-hardening-subspec-d/spec.md`
**Umbrella**: `specs/016-security-hardening/` (shared research/contracts refined by this feature's local artifacts — local prevails where they differ; see research.md)

## Summary

Move the SPA's session credentials out of script-readable storage: the refresh token becomes an `HttpOnly; Secure` cookie set by the backend (config-driven `SameSite` — `Strict` in prod/Dev, `None` for cross-site PR previews), the access token lives in memory only, and the session re-hydrates via a hint-gated silent refresh at startup with cross-tab single-flight (Web Locks + BroadcastChannel) honoring the backend's strict one-time-use rotation. Ship an enforcing CSP via a repo-controlled Cloudflare Pages `_headers` file whose API origin is stamped per build. Remove the committed e2e auth-state file (real replayable refresh token) and invalidate it; scope the bearer interceptor to the API origin; fix opener ordering, base64url expiry parsing, and starter-template/dead-control cruft.

## Technical Context

**Language/Version**: TypeScript / Angular 17.3 (frontend); C# / .NET 9.0 (backend cookie endpoints)
**Primary Dependencies**: Angular signals/standalone APIs, RxJS, ngx-stripe (CSP origins), Web Locks API + BroadcastChannel (cross-tab refresh); FastEndpoints, ASP.NET Identity/JWT (existing), FluentValidation (new `Auth:RefreshCookie` options)
**Storage**: N/A — no schema changes; refresh tokens already persisted hashed in PostgreSQL
**Testing**: Karma/Jasmine unit (token service, interceptor, startup hint), Cypress e2e (login → reload → silent refresh; CSP-compatible payments), Playwright deployed smoke (unchanged flows must pass under cookie auth), xUnit + Testcontainers integration (`AuthCookieTests`: set/rotate/clear cookie, Origin check, body omits refreshToken)
**Target Platform**: Cloudflare Pages (static host, `_headers` support) + Cloud Run API
**Project Type**: Web application (existing `neko-hoa/` frontend + `HOAManagementCompany/` backend)
**Performance Goals**: No startup regression for anonymous visits (hint gate ⇒ zero extra calls); one refresh round-trip for returning users before first protected render
**Constraints**: Backend statelessness preserved (cookie only transports the already-persisted token); strict one-time-use rotation unchanged; enforcing CSP must not break Stripe payments or API/telemetry calls; e2e/CI must keep passing without the committed auth-state file
**Scale/Scope**: ~6 frontend files + 1 new script + `_headers`; 3 backend endpoints + 1 options class; 2 CI workflow touch-points (headers stamping); no migrations

## Constitution Check

- **Technology fit**: PASS — Angular on Cloudflare Pages, FastEndpoints for the auth endpoints, in-application Identity/JWT auth (rotating single-use hashed refresh tokens preserved), GitHub Actions. No new services. Sentry: not present in the frontend today; adding it is out of scope here (CSP will need a Sentry origin if/when added — noted, not a violation introduced by this feature).
- **HOA tenancy**: PASS — no data-model or authorization changes; session transport only.
- **API contracts**: PASS — breaking change (refreshToken removed from login/refresh response bodies; refresh input moves to cookie) documented in `contracts/auth-session.md` with migration note: frontend + backend ship atomically in one PR; responses marked never-cacheable.
- **Security and operations**: PASS — removes script-readable credential; secrets not committed (tracked auth-state file removed + token invalidated); new cookie options validated at startup via FluentValidation (fail-fast, environment-aware); no logging of token material.
- **File storage**: N/A.
- **Caching/edge**: PASS — auth responses explicitly never edge-cached (contract); `_headers` adds no caching directives for HTML; hashed static assets unchanged.
- **Testing discipline**: PASS — test-first: backend `AuthCookieTests` (Testcontainers, transaction-isolated, Theories for SameSite config matrix), Karma specs for token service/interceptor/initializer, Cypress session-security e2e; Playwright deployed smoke unchanged-but-must-pass.
- **CI/CD and documentation**: PASS — Sonar/Codecov run as configured; 95% changed-line coverage target applies; Repowise marker refresh task included; headers stamping wired into existing deploy-dev and pr-env workflows.
- **Executable & living specs**: PASS — every FR maps to a named test (see Test Map); spec.md updated pre-PR if implementation diverges; supersessions of umbrella artifacts recorded in research.md (corpus consistency).

**Gate result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/020-security-hardening-subspec-d/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D-R1..D-R7 (refines umbrella §D)
├── data-model.md        # Phase 1 — client-side session state + cookie shape (no DB changes)
├── quickstart.md        # Phase 1 — build/verify walkthrough incl. token invalidation ops step
├── contracts/
│   └── auth-session.md  # Phase 1 — cookie-based auth contract (supersedes umbrella copy)
└── tasks.md             # Phase 2 (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
HOAManagementCompany/
├── Features/Auth/
│   ├── LoginEndpoint.cs            # set refresh cookie; drop refreshToken from body
│   ├── RefreshEndpoint.cs          # read cookie (not body); rotate + re-set; Origin check
│   ├── LogoutEndpoint.cs           # revoke + clear cookie
│   └── AuthService.cs              # response shape change only (rotation untouched)
├── Infrastructure/Configuration/
│   ├── RefreshCookieOptions.cs     # NEW: Auth:RefreshCookie {SameSite, Domain?} + validator
│   └── RefreshCookieOptionsValidator.cs
└── Program.cs                      # options registration (AddValidatedOptions)

HOAManagementCompany.Tests/
└── Integration/Security/AuthCookieTests.cs   # NEW (test-first)

neko-hoa/
├── src/app/core/services/token.service.ts        # in-memory access token; base64url fix; has-session hint
├── src/app/core/services/auth.service.ts         # drop localStorage persistence; cookie-based flows
├── src/app/core/interceptors/auth.interceptor.ts # API-origin scoping; single-flight + cross-tab lock
├── src/app/core/services/session-refresh.ts      # NEW: Web Locks/BroadcastChannel coordinator + APP_INITIALIZER factory
├── src/app/app.config.ts                          # register hint-gated silent-refresh APP_INITIALIZER
├── src/app/app.component.html                     # remove starter template
├── src/app/features/auth/login/*                  # remove dead Google button
├── src/app/features/community/documents/documents.component.ts  # opener ordering
├── src/assets/_headers                            # NEW: enforcing CSP + baseline headers (placeholder origin)
├── scripts/stamp-headers.mjs                      # NEW: post-build __API_ORIGIN__ stamping (fails if unstamped)
└── e2e/.auth/state.json                           # REMOVED from tracking; e2e/.auth/ ignored

.github/workflows/test.yml     # deploy-dev: stamp _headers with Dev API origin before Pages deploy
.github/workflows/pr-env.yml   # stamp _headers with the PR's API origin (alongside existing env sed)
```

**Structure Decision**: Existing two-project web layout; no new projects. Frontend changes concentrate in `core/` auth plumbing plus one asset + one script; backend changes are confined to the three auth endpoints and one validated options class.

## Test Map (FR → executable test)

| FR | Test |
|----|------|
| FR-D1 | `AuthCookieTests` (cookie attrs, body omits token); Cypress `session-security.cy.ts` (login → reload → re-hydrated; token not in storage) |
| FR-D2 | `_headers` stamped-content assertion + deployed-response header check in Playwright smoke |
| FR-D3 | Repo scan assertion (no tracked `e2e/.auth/`); quickstart ops step invalidates old token |
| FR-D4 | `auth.interceptor.spec.ts` (bearer only for `environment.apiBaseUrl`) |
| FR-D5 | `auth.interceptor.spec.ts` + `session-refresh` spec (concurrent 401s ⇒ 1 refresh; lock respected) |
| FR-D6 | `token.service.spec.ts` (base64url); `documents.component` spec (opener nulled pre-navigation) |
| FR-D7 | Login/app component specs assert removed content absent |
| FR-D8 | Existing boot-guard spec remains green |

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Features/Auth/RefreshEndpoint.cs` | `domain=auth-session` | Cookie-based refresh contract + rotation |
| `neko-hoa/src/app/core/interceptors/auth.interceptor.ts` | `domain=frontend-session` | Origin scoping + single-flight/cross-tab refresh |
| `neko-hoa/src/assets/_headers` (adjacent doc) | `section=csp` | CSP origin rationale |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Risks & Coordination

1. **017-A interplay**: this branch pre-dates PR #100; both touch `Features/Auth` + `Program.cs`. Sequence: rebase onto (or merge) A before implementation of the backend tasks, or accept a small conflict at merge. A + D must reach any shared environment together.
2. **Signup-UI gap (flagged)**: the multi-step registration UI consuming A's new `/auth/register` contract is specced nowhere (not in D, not in umbrella tasks). Must be added as a D spec amendment or micro-spec before A+D merge; not planned here.
3. **CSP breakage**: mitigated by exact-origin stamping + Cypress payment flow under the served policy in CI before deploy.
4. **PR-preview cookies**: `SameSite=None` required there; covered by config-driven attribute + existing per-PR Playwright login smoke as the live proof.

## Complexity Tracking

No constitution violations — table not required.

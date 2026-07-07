# Research: Sub-Spec D — Frontend Session & Content Security

**Date**: 2026-07-07 · **Branch**: `020-security-hardening-subspec-d`
Refines the umbrella research (`specs/016-security-hardening/research.md` §Sub-spec D) with the 2026-07-04 clarifications and verified current-state facts. Where this document and the umbrella disagree, this document prevails (recorded per constitution §11 cross-spec consistency).

## Verified current state (2026-07-07 inventory)

| Item | Fact |
|------|------|
| Token storage | `src/app/core/services/token.service.ts` writes `neko_token`, `neko_refresh`, `neko_user` to **localStorage** |
| Interceptor | `src/app/core/interceptors/auth.interceptor.ts` attaches the bearer to **every** request; 401-refresh via `switchMap`, **not** single-flight |
| Expiry parsing | `token.service.ts` `isTokenExpired` uses plain `atob()` — fails on base64url payloads |
| Committed credential | `neko-hoa/e2e/.auth/state.json` is **tracked** and contains a real access + refresh token; `.gitignore` does not cover it; `e2e/global-setup.ts` regenerates it at runtime (line 27) |
| Opener flow | `documents.component.ts` sets `tab.location.href = url` **then** `tab.opener = null` — ordering is the finding (FR-D6); fallback path already uses `noopener,noreferrer` |
| CSP | No `_headers`/`_redirects` file exists; no CSP anywhere |
| API origins | prod `https://api.nekohoa.com`, Dev `https://api-dev.nekohoa.com`, local `http://localhost:5212`, PR preview `${apiOrigin}` sed-stamped into `environment.pr-preview.ts` by `pr-env.yml` |
| Third-party runtime origins | Stripe only (`js.stripe.com` script/frames via ngx-stripe; `api.stripe.com` XHR from Stripe.js). No frontend Sentry today. Telemetry goes to the API origin |
| Starter cruft | `app.component.html` ships the full Angular CLI starter template above `<router-outlet/>`; login page has a non-functional "Continue with Google" button |
| Backend | `LoginEndpoint`/`RefreshEndpoint`/`LogoutEndpoint` exist; refresh rotation is strict one-time-use (used token deleted, no grace window); CORS already runs `AllowCredentials()` with origin allowlist + suffixes |

## D-R1: Refresh-cookie attributes — config-driven SameSite

- **Decision**: `Set-Cookie: neko_refresh=<token>; HttpOnly; Secure; SameSite=<config>; Path=/api/v1/auth; Max-Age=2592000` (30 days, matching `Jwt:RefreshTokenExpiryDays` — clarified 2026-07-04). `SameSite` comes from validated config (`Auth:RefreshCookie:SameSite`): **`Strict` in Production** (frontend and API share the `nekohoa.com` registrable domain), **`None` in Dev and PR environments** — Dev because the pre-promotion Playwright smoke logs in from `*.nekohoa-dev.pages.dev` **preview** origins against the Dev API (cross-site; this is why `Cors:AllowedOriginSuffixes` exists, see PR #66), and PR envs because `pr-N.nekohoa-dev.pages.dev` → `*.run.app` is cross-site — and `Lax` locally (`localhost:4200` → `localhost:5212` is same-site).
- **CSRF defense**: `/api/v1/auth/refresh` verifies the request `Origin` (fallback `Referer`) against the configured CORS allowlist/suffixes and rejects otherwise. Required for the `None` environments, harmless for `Strict`. JSON-only endpoints already force CORS preflight; this is defense-in-depth.
- **Rationale**: The umbrella contract picked blanket `Strict` on the assumption every environment is same-site; the verified origins show PR previews are not, and the per-PR smoke logs in for real. Config-driven keeps the strongest setting where it is valid without breaking ephemeral envs.
- **Alternatives**: Blanket `SameSite=None` (rejected: needlessly weakens prod/Dev); fronting per-PR APIs under `nekohoa.com` subdomains (rejected: per-PR DNS/certificate machinery far out of scope).
- **Supersedes**: umbrella `contracts/auth-session.md` cookie note and task T096 (blanket `Strict`); `Path=/auth` corrected to `/api/v1/auth` (actual route prefix).

## D-R2: Session re-hydration — hint-gated APP_INITIALIZER (clarified 2026-07-04)

- **Decision**: A non-sensitive marker `localStorage['neko_has_session'] = '1'` is set on successful login/refresh and cleared on logout or a failed startup refresh. An `APP_INITIALIZER` (after the existing config guard) calls `POST /api/v1/auth/refresh` with `withCredentials: true` **only when the marker is present**, storing the returned access token in memory before the router renders protected routes.
- **Rationale**: Clarification. Anonymous visitors make no doomed 401 call; returning users are never bounced to login on reload. localStorage marker over a readable cookie: no extra Set-Cookie surface, trivially testable, contains no credential material (its theft reveals only "probably logged in").
- **Alternatives**: Unconditional startup refresh (rejected in clarification); lazy refresh-on-first-401 (rejected: logged-out flash).

## D-R3: Single-flight refresh — cross-tab via Web Locks (clarified 2026-07-04)

- **Decision**: In-tab: one shared refresh observable (all concurrent 401s wait on it). Cross-tab: the refresh call runs inside `navigator.locks.request('neko-refresh', …)`; the winning tab refreshes and broadcasts the new access token + expiry over a `BroadcastChannel('neko-auth')`; other tabs adopt the broadcast token instead of refreshing. If `navigator.locks` is unavailable (out-of-support browsers), degrade to per-tab single-flight — the strict-rotation race then only affects multi-tab edge cases on legacy browsers.
- **Rationale**: Clarification chose cross-tab coordination with backend rotation semantics unchanged. Web Locks is supported by all evergreen browsers and is the purpose-built primitive; BroadcastChannel distributes the result so N tabs cost one rotation.
- **Alternatives**: Backend reuse grace window (rejected in clarification — weakens one-time-use); localStorage lock protocols (rejected: hand-rolled, race-prone).

## D-R4: CSP — enforcing `_headers`, API origin stamped per build (clarified 2026-07-04)

- **Decision**: Add `neko-hoa/src/assets/_headers` (copied by the existing `src/assets` asset rule into `dist/neko-hoa/browser/_headers`, which Cloudflare Pages consumes) containing an enforcing policy:
  - `default-src 'self'`
  - `script-src 'self' https://js.stripe.com`
  - `frame-src https://js.stripe.com https://hooks.stripe.com`
  - `connect-src 'self' __API_ORIGIN__ https://api.stripe.com`
  - `style-src 'self' 'unsafe-inline'` (Angular component styles are injected `<style>` tags)
  - `img-src 'self' data:`, `font-src 'self'`, `object-src 'none'`, `base-uri 'self'`, `frame-ancestors 'none'`
  - plus baseline headers: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`
  `__API_ORIGIN__` is stamped post-build by a small node script (`neko-hoa/scripts/stamp-headers.mjs`, same pattern as the existing `generate-build-id.mjs`) from the built environment's API origin; CI already knows the origin in both `test.yml` (deploy-dev) and `pr-env.yml`. The script fails the build if the placeholder is left unstamped.
- **Rationale**: Clarifications (repo-controlled file, enforcing day one, exact origin per deployment, no wildcards, PR previews same posture). Stripe origins per Stripe's official CSP guidance; the app's only other runtime origin is the API itself (telemetry included).
- **Alternatives**: Static allowlist + `*.run.app` wildcard (rejected in clarification); `<meta http-equiv>` CSP (rejected: cannot express `frame-ancestors`, weaker than headers).
- **Test**: Karma/na — asserted by (a) a Playwright/e2e check that the deployed response carries the CSP and (b) a cheap unit assertion that the built `_headers` contains no `__API_ORIGIN__` placeholder and payments/API origins are present.

## D-R5: Committed auth-state removal + token invalidation

- **Decision**: `git rm --cached neko-hoa/e2e/.auth/state.json`; ignore `e2e/.auth/`; `global-setup.ts` already regenerates state at runtime so CI is unaffected. The committed refresh token is invalidated by revoking all refresh tokens for the seed user in the Dev database (ops step — the token hash store is env-local; document in quickstart). The committed access token is expired by time; nothing to rotate.
- **Rationale**: FR-D3/SC-D3. History rewrite is not attempted — invalidation, not erasure, is the control (matches A's FR-A9 precedent).

## D-R6: Minor hardening (FR-D6/D7)

- **Decision**: (a) `documents.component.ts`: null the opener **before** assigning `location.href` (swap two lines). (b) `isTokenExpired`: base64url-normalize before `atob` (`replace(/-/g,'+').replace(/_/g,'/')` + padding). (c) Delete starter-template markup from `app.component.html` (keep `<router-outlet/>`), remove the non-functional "Continue with Google" button from the login page. Existing boot-time publishable-key guard untouched (FR-D8).

## D-R7: Backend surface

- **Decision**: `LoginEndpoint` sets the cookie and stops returning `refreshToken` in the body; `RefreshEndpoint` reads the cookie (body fallback removed), rotates, re-sets the cookie; `LogoutEndpoint` revokes and clears the cookie (`Max-Age=0`). New `Auth:RefreshCookie` options class with FluentValidation (SameSite ∈ {Strict, Lax, None}, None⇒Secure required) per constitution §8. No schema changes (refresh tokens already persisted hashed).
- **Breaking-contract note**: removing `refreshToken` from the login/refresh response body is a breaking API change shipped atomically with the frontend in this same PR; the e2e suite covers the coordinated behavior. Documented in `contracts/auth-session.md` (D-local, supersedes the umbrella copy).

## Merge-order note (017-A interplay)

This branch forked from `main` before 017-A (PR #100). Both touch `Features/Auth` (A: register/lockout/DI; D: login/refresh/logout cookies) — distinct methods, small expected conflicts in `Program.cs`/`AuthService.cs`. Plan: land D after A (or rebase D onto A pre-merge). **Gap flagged**: the multi-step signup UI consuming A's new `/auth/register` contract (verification proof + claim code) is in neither A, D, nor the umbrella tasks — it must be added (D spec amendment or micro-spec) before A+D can merge to a shared environment.

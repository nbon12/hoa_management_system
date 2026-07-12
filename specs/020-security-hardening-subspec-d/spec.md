# Sub-Spec D: Frontend Session & Content Security

**Feature Branch**: `020-security-hardening-subspec-d`
**Split from**: [016 umbrella](../016-security-hardening/spec.md) — shared plan/tasks/research/data-model/contracts live there.
**Created**: 2026-07-01
**Status**: Draft

## Overview

The Angular frontend has a clean XSS posture (no unsafe HTML sinks) and a textbook-correct Stripe integration (card data only ever entered into the provider's hosted element; only publishable keys in the repo). The findings concern **session-credential exposure and content-security containment**: access and refresh tokens plus user PII are persisted in browser storage readable by any script; there is no Content-Security-Policy to contain a compromised/injected script; a test authentication-state file containing a real refresh token is committed to the repository; and the auth request interceptor attaches the bearer token to every request without scoping it to the API origin.

## Clarifications

### Session 2026-07-04

- Q: On app startup/reload, how should the frontend re-hydrate the session from the HttpOnly refresh cookie? → A: Hinted refresh — attempt silent refresh at startup only when a non-sensitive "has-session" hint (readable marker cookie or localStorage flag, no credential material) is present; anonymous visitors skip the call; returning users re-hydrate before protected routes render.
- Q: How should concurrent refresh across multiple tabs be handled, given strict one-time-use refresh-token rotation? → A: Cross-tab lock — the frontend coordinates refreshes across tabs (Web Locks / BroadcastChannel) so only one tab refreshes and shares the result; backend rotation semantics stay strict (no grace window).
- Q: Should the refresh-token cookie persist across browser restarts, and for how long? → A: Persistent cookie, Max-Age = existing 30-day refresh-token lifetime — preserves current "stay signed in" UX; no remember-me UI in this slice.
- Q: How should the enforced CSP handle per-environment API origins (Dev vs per-PR envs)? → A: Per-build injection — the pipeline stamps each deployment's exact API origin into the headers file; no wildcards; PR previews keep the same enforcing posture as Dev.

### Session 2026-07-08

- Q: Where does the signup UI consuming sub-spec A's reworked `/auth/register` contract (verification proof + claim code) live? It was specced nowhere — a program gap found during /speckit.analyze and A/D merge planning. → A: Amended into this sub-spec as User Story 4: the registration flow is frontend session security surface, and A+D must land together anyway. Includes the non-production, secret-gated e2e code-retrieval seams needed to keep the deployed registration e2e alive (codes are stored hashed and are otherwise irretrievable).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Contain credential theft from script compromise (Priority: P1)

A script that runs in the app (including a compromised third-party dependency) cannot read a long-lived refresh token, and a Content-Security-Policy limits what injected/compromised script can do or exfiltrate.

**Why this priority**: The access token, the **long-lived refresh token**, and user PII are stored in script-readable browser storage. Any XSS or supply-chain compromise reads all three in one line and exfiltrates a refresh token, yielding durable account takeover on a payments-capable portal — and there is no CSP to contain the injected script. This is the highest-impact frontend finding.

**Independent Test**: Confirm the refresh token is not present in script-readable storage after login. Confirm a CSP is served that restricts script/connect/frame sources to the required origins (self, API, payment provider).

**Acceptance Scenarios**:

1. **Given** an authenticated session, **When** page scripts inspect browser-accessible storage, **Then** the refresh token is not retrievable by script.
2. **Given** the deployed frontend, **When** a response is inspected, **Then** a Content-Security-Policy is present that restricts script, connect, and frame sources to the approved origins.

---

### User Story 2 - Remove committed credentials and scope token transmission (Priority: P2)

No real authentication token is stored in the repository, and the bearer token is attached only to requests targeting the application's own API origin.

**Why this priority**: A committed test auth-state file contains a real (replayable) refresh token and a known-good token structure. Separately, the interceptor attaches the bearer token to every outbound request regardless of destination — today all calls target the API, but the first future call to a third-party origin would silently ship the user's token.

**Independent Test**: Confirm the auth-state file is untracked and its token invalidated. Confirm the interceptor attaches the token only when the request targets the configured API origin.

**Acceptance Scenarios**:

1. **Given** the repository, **When** it is scanned, **Then** no real authentication or refresh token is tracked, and any previously committed token has been invalidated.
2. **Given** an outbound request to a non-API origin, **When** the interceptor runs, **Then** it does not attach the bearer token.
3. **Given** concurrent unauthorized responses, **When** the client refreshes its session, **Then** it issues a single shared refresh rather than parallel refreshes with the same token.

---

### User Story 3 - Minor content and navigation hardening (Priority: P3)

Document-open and outbound-link flows follow safe ordering/attributes, token parsing tolerates all valid token encodings, and shipped bundles do not contain dead starter-template content or non-functional auth controls.

**Why this priority**: Low-impact robustness and hygiene: a document-open flow nulls the opener after navigation rather than before; token expiry parsing uses standard base64 rather than the URL-safe variant (fails closed but can log users out spuriously); and starter-template cruft plus a non-functional third-party sign-in button remain in the bundle.

**Independent Test**: Verify safe opener handling and link attributes; verify token parsing handles URL-safe encodings; verify removed dead content.

**Acceptance Scenarios**:

1. **Given** a document-open action, **When** a new tab is opened, **Then** the opener reference is cleared safely and fallback paths use no-opener/no-referrer.
2. **Given** a token whose encoding uses URL-safe characters, **When** expiry is evaluated, **Then** it is parsed correctly and a valid session is not spuriously discarded.

---

### Edge Cases

- The HttpOnly-cookie end-state requires backend cooperation (a cookie-setting login/refresh response and a silent-refresh endpoint); this backend work is in scope. Startup re-hydration is **hint-gated** (clarified 2026-07-04): a non-sensitive "has-session" marker (readable cookie or localStorage flag containing no credential material) gates the silent-refresh call — present → refresh before protected routes render; absent → no call, user is anonymous. Users are therefore not logged out on reload, and anonymous visits make no doomed refresh call.
- The CSP must allow the payment provider's script and frame origins and the API connect origin, or payments and API calls break.
- Invalidating the committed refresh token must not disrupt legitimate CI e2e runs that regenerate their own auth state at runtime.

### User Story 4 - Register with verified email and claim code (Priority: P2, amended 2026-07-08)

As a new resident, I complete registration by proving control of my email address and presenting the claim code the HOA delivered to the owner contact on file, so that property claiming is entitled rather than guessable (sub-spec A's FR-A1 contract) and I land signed in.

**Why this priority**: Sub-spec A removed account-number-only claiming from the backend; without this story the shipped signup UI posts a contract the backend rejects — A+D cannot reach a shared environment.

**Independent Test**: Through the UI: request a code for an email, enter the delivered code, submit names/password/claim code — account is created, session established (cookie), dashboard renders. Wrong or missing codes produce only generic failures.

**Acceptance Scenarios**:

1. **Given** an unclaimed property with an issued claim code, **When** a user completes email verification and submits valid registration details with that claim code, **Then** the account is created, the session is established via the HttpOnly-cookie flow, and the dashboard renders.
2. **Given** any invalid input (wrong verification code, expired proof, wrong/used claim code), **When** the user submits, **Then** the UI shows a generic failure with no hint distinguishing which element failed.
3. **Given** the deployed Dev/PR e2e suite, **When** the registration test runs, **Then** it obtains its codes only through non-production, secret-gated test-support endpoints (never from production surfaces or logs).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-D1**: The long-lived refresh token MUST NOT be stored in script-readable browser storage. The target end-state is: the refresh token is set by the backend in an **HttpOnly, Secure, SameSite cookie**; the access token is held in memory only; and the session is re-hydrated via a silent refresh on startup. *(Clarified 2026-07-02: the full HttpOnly-cookie end-state is required — not an interim "stop persisting" or frontend-only stopgap. Backend endpoint work to set the cookie and serve silent refresh is in scope.)* *(Clarified 2026-07-04: the cookie is persistent with Max-Age matching the existing 30-day refresh-token lifetime — preserving current "stay signed in" behavior; startup re-hydration is hint-gated per the Clarifications session.)* *(Implemented 2026-07-08: cookie transport covers every token-minting flow — login, register, refresh, and property switch all set/rotate the cookie and omit the token from response bodies.)*
- **FR-D2**: The deployed frontend MUST serve a Content-Security-Policy that restricts script, connect, and frame sources to the approved origins (self, API origin, payment provider), plus the other headers defined in the shared baseline. *(Clarified 2026-07-02: the CSP MUST be delivered via a repo-controlled build-output headers file and asserted by an automated test — not configured only in the host dashboard. It ships in **enforcing** mode from day one — no report-only phase — given the app has no unsafe HTML sinks and a known origin set.)* *(Clarified 2026-07-04: the API origin in `connect-src` is stamped per build — each deployment (Dev, Pages preview, per-PR env) gets its exact API origin injected into the headers file by the build pipeline, mirroring the existing environment-file injection; no wildcard origins, and PR previews carry the same enforcing posture as Dev.)*
- **FR-D3**: No real authentication or refresh token MUST be tracked in the repository; the committed test auth-state file MUST be removed from tracking and ignored, and any previously committed refresh token MUST be invalidated.
- **FR-D4**: The auth interceptor MUST attach the bearer token only to requests whose target is the configured API origin.
- **FR-D5**: The session-refresh flow MUST use a single-flight mechanism so concurrent unauthorized responses trigger one refresh rather than parallel refreshes reusing the same token. *(Clarified 2026-07-04: single-flight is cross-tab, not just per-tab — the client coordinates across browser tabs (e.g. Web Locks / BroadcastChannel) so only one tab performs the refresh and shares the outcome. The backend's strict one-time-use rotation is unchanged; no reuse grace window is introduced.)*
- **FR-D6**: Document-open and outbound-link flows MUST use safe opener handling and no-opener/no-referrer attributes; token-expiry parsing MUST correctly handle URL-safe token encodings.
- **FR-D7**: Dead starter-template content and non-functional authentication controls MUST be removed from shipped bundles.
- **FR-D8**: Only publishable payment-provider keys may appear in frontend configuration; the existing boot-time guard that refuses to start without the production publishable key MUST be preserved.
- **FR-D9**: The registration UI MUST implement the multi-step flow of the reworked register contract — request email verification, confirm the delivered code (yielding a proof), then submit registration details including the claim code — and MUST NOT offer an account-number-only claiming path. *(Amended 2026-07-08 — closes the A/D signup gap.)*
- **FR-D10**: Registration failure messaging in the UI MUST stay generic (mirroring the backend's uniform responses) — no message may distinguish whether the email, verification code, or claim code was at fault.
- **FR-D11**: E2E test support for registration (retrieving a claim code or delivered verification code) MUST be available only via endpoints gated exactly like the e2e cleanup endpoint (config flag + shared secret + hard-blocked in Production/Staging); raw codes MUST NOT be exposed through any production surface or log.

### Key Entities

- **Session credentials**: Access token (in-memory) and refresh token (script-inaccessible), replacing script-readable storage.
- **Content-Security-Policy**: The served policy restricting content sources.
- **Auth interceptor**: Attaches the bearer token only to API-origin requests, with single-flight refresh.

### Security & Abuse Controls *(constitution subset)*

- **Authorization**: Frontend guards remain UX-only; the backend continues to enforce authorization. This sub-spec reduces the value of a stolen frontend session, not the authorization model.
- **Security and abuse controls**: CSP as the primary containment for injected/compromised script; no credentials in source; token transmission scoped to the API origin.
- **Observability**: Telemetry propagation remains scoped to the API origin (already correct); no tokens in URLs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-D1**: After login, the refresh token is not retrievable by page script in 100% of tested flows.
- **SC-D2**: The deployed frontend serves a CSP restricting script/connect/frame to approved origins, verified by inspecting responses; payments and API calls continue to function.
- **SC-D3**: A repository/history scan finds 0 tracked real authentication/refresh tokens after remediation; the previously committed token is invalidated.
- **SC-D4**: The bearer token is attached only to API-origin requests, verified by test; concurrent 401s produce exactly one refresh call.
- **SC-D5**: Token-expiry parsing correctly handles URL-safe encodings (no spurious logouts), verified by test.

## Assumptions

- Backend support for an HttpOnly refresh-token cookie and a silent-refresh endpoint is in scope for this program (per the 2026-07-02 clarification the full cookie end-state is required, not an interim stopgap).
- The frontend is deployed on a static host where response headers are configured via a **repo-controlled build-output headers file**; the CSP is delivered there and asserted by an automated test (per the 2026-07-02 clarification, not the host dashboard).
- The CI e2e suite regenerates its own auth state at runtime, so removing the committed file does not break it.
- The Stripe/publishable-key posture is already correct and is preserved, not changed.

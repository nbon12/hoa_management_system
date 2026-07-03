# Sub-Spec D: Frontend Session & Content Security

**Feature Branch**: `020-security-hardening-subspec-d`
**Split from**: [016 umbrella](../016-security-hardening/spec.md) — shared plan/tasks/research/data-model/contracts live there.
**Created**: 2026-07-01
**Status**: Draft

## Overview

The Angular frontend has a clean XSS posture (no unsafe HTML sinks) and a textbook-correct Stripe integration (card data only ever entered into the provider's hosted element; only publishable keys in the repo). The findings concern **session-credential exposure and content-security containment**: access and refresh tokens plus user PII are persisted in browser storage readable by any script; there is no Content-Security-Policy to contain a compromised/injected script; a test authentication-state file containing a real refresh token is committed to the repository; and the auth request interceptor attaches the bearer token to every request without scoping it to the API origin.

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

- The HttpOnly-cookie end-state requires backend cooperation (a cookie-setting login/refresh response and a silent-refresh endpoint); this backend work is in scope, and the silent-refresh-on-startup behavior must be defined so users are not logged out on reload.
- The CSP must allow the payment provider's script and frame origins and the API connect origin, or payments and API calls break.
- Invalidating the committed refresh token must not disrupt legitimate CI e2e runs that regenerate their own auth state at runtime.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-D1**: The long-lived refresh token MUST NOT be stored in script-readable browser storage. The target end-state is: the refresh token is set by the backend in an **HttpOnly, Secure, SameSite cookie**; the access token is held in memory only; and the session is re-hydrated via a silent refresh on startup. *(Clarified 2026-07-02: the full HttpOnly-cookie end-state is required — not an interim "stop persisting" or frontend-only stopgap. Backend endpoint work to set the cookie and serve silent refresh is in scope.)*
- **FR-D2**: The deployed frontend MUST serve a Content-Security-Policy that restricts script, connect, and frame sources to the approved origins (self, API origin, payment provider), plus the other headers defined in the shared baseline. *(Clarified 2026-07-02: the CSP MUST be delivered via a repo-controlled build-output headers file and asserted by an automated test — not configured only in the host dashboard. It ships in **enforcing** mode from day one — no report-only phase — given the app has no unsafe HTML sinks and a known origin set.)*
- **FR-D3**: No real authentication or refresh token MUST be tracked in the repository; the committed test auth-state file MUST be removed from tracking and ignored, and any previously committed refresh token MUST be invalidated.
- **FR-D4**: The auth interceptor MUST attach the bearer token only to requests whose target is the configured API origin.
- **FR-D5**: The session-refresh flow MUST use a single-flight mechanism so concurrent unauthorized responses trigger one refresh rather than parallel refreshes reusing the same token.
- **FR-D6**: Document-open and outbound-link flows MUST use safe opener handling and no-opener/no-referrer attributes; token-expiry parsing MUST correctly handle URL-safe token encodings.
- **FR-D7**: Dead starter-template content and non-functional authentication controls MUST be removed from shipped bundles.
- **FR-D8**: Only publishable payment-provider keys may appear in frontend configuration; the existing boot-time guard that refuses to start without the production publishable key MUST be preserved.

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

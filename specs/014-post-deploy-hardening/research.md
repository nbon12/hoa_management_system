# Phase 0 Research: Hardening not addressed by ephemeral environments

All spec-level NEEDS CLARIFICATION were resolved during `/speckit.clarify` (Session 2026-06-21). The remaining open questions are implementation-mechanism choices, resolved below.

## R1 — Trusted-edge verification: how do we know a request really came through Cloudflare?

**Decision**: Trust `CF-Connecting-IP` **only** when the request carries a configured shared-secret edge header (`RateLimiting:TrustedEdge:SecretHeaderName` / `…:SecretHeaderValue`), injected by a Cloudflare Transform Rule at the edge. When the header is absent or the value does not match, the request is treated as **un-attributable** (→ `"unknown"` partition). The secret is supplied to the app via Cloud Run secret/env config; it is never returned to clients.

**Rationale**:
- On Cloud Run, `HttpContext.Connection.RemoteIpAddress` is Google's front-end address, **not** Cloudflare's, so verifying the connecting IP against published Cloudflare ranges is unreliable for this topology. A request-borne shared secret that only the edge can set is the robust signal that the request transited the trusted edge.
- It is simple, deterministic, and directly testable (set/omit/mismatch the header in `WebApplicationFactory` tests) — satisfying acceptance scenario 4 and SC-003 without external infrastructure.
- It composes with the existing config-driven pattern (`StartupOptions`, `Cors:*`, `Observability:*`): the secret is just another bound option, with no value committed to source control.

**Alternatives considered**:
- *`UseForwardedHeaders` + Cloudflare IP ranges as `KnownProxies/KnownNetworks`*: standard, but the Cloud Run hop masks the Cloudflare connecting IP, so the "known proxy" check evaluates Google's address; brittle and topology-dependent. Rejected as the primary mechanism (kept as a documented fallback if origin-IP visibility changes).
- *Cloudflare Authenticated Origin Pulls (mTLS)*: strongest, but terminates at the Cloud Run ingress/load balancer, not visible as an app-level per-request signal without extra plumbing; heavier than needed for rate-limit attribution. Rejected for this slice.
- *Trust `CF-Connecting-IP` unconditionally*: trivially forgeable by a client hitting the origin directly. Rejected (this is the exact vulnerability FR-002 forbids).

## R2 — Partition keys per limiter

**Decision** (from clarification): `auth` limiter → resolved client IP from `CF-Connecting-IP`; `payments` limiter → authenticated user identity (JWT subject / owner id already on `HttpContext.User`). Un-attributable requests for either → shared `"unknown"` partition with its own quota.

**Rationale**: Auth endpoints (login/refresh) run before the caller is identified, so IP is the only available per-client signal. Payment endpoints are authenticated, so the user identity is a precise, NAT-immune, IP-rotation-resistant key. Documented in the spec's NAT edge case.

**Alternatives considered**: IP for both (NAT users share a payment bucket — rejected); composite user+IP for payments (adds no security over user-only but reintroduces NAT fragility — rejected).

## R3 — Default thresholds (per environment)

**Decision**: Keep conservative production defaults, env-tunable via config (FR-004). Starting values:

| Policy | Partition | Prod default (per minute) | Dev override | Existing config key |
|--------|-----------|---------------------------|--------------|---------------------|
| `auth` | client IP | 20 | higher (e.g. 100) | `RateLimiting:AuthPermitsPerMinute` (already exists, default 10 → raise to 20 now that it's per-client) |
| `payments` | user id | 30 | higher | `RateLimiting:PaymentsPermitsPerMinute` (new, default 20) |
| `unknown` | shared | 30 (strict, shared) | n/a | `RateLimiting:UnknownPermitsPerMinute` (new, default 30) |

**Rationale**: Because limits are now per-client, the old global ceilings (10/20) can be raised to comfortable per-user values without weakening abuse protection. The `auth` permit key already exists and is honored by Dev to keep the Playwright login bursts from 429-ing; it remains the tuning knob. Exact numbers are tunable and confirmed in `tasks.md`/PR; they do not affect architecture.

**Alternatives considered**: Hard-coded constants (violates FR-004 — rejected). Sliding-window/token-bucket (the codebase standard is fixed-window via `AddFixedWindowLimiter`; staying consistent — fixed-window retained).

## R4 — Curated smoke set membership (US2)

**Decision**: Introduce a `@smoke` tag (mirroring the existing `@local-only` tag convention) on a small set of **read-only** deployment-health checks, and run the post-deploy gate with `--grep @smoke`. Candidate smoke checks (all read-only, no shared-state mutation):
- Portal/login page renders (anonymous).
- Authenticated app shell + dashboard renders (uses the seed-user storage state from `global-setup`; login issues a token but mutates no business data).
- Key authenticated pages render without error (e.g., statement/property/community list views — read-only loads, no submit).
- API reachability/health responds.

**Explicitly excluded** from `@smoke` (remain in the full suite only): registration (claims a property), auto-pay toggling (disables seed enrollment durably), poll-vote, RSVP, payment submission, and any `@local-only` Stripe-iframe specs.

**Rationale**: Satisfies FR-005/FR-006/SC-004/SC-005 — fast, deterministic, no persistent side effects, and no reliance on the `/e2e/cleanup` endpoint (the smoke set never registers a user, so cleanup is irrelevant to it). Still fails loudly on real breakage (auth down, pages not rendering, API unreachable) per the edge case and acceptance scenario 5. Tag-based selection keeps the full regression suite intact and runnable (`npm run e2e`).

**Alternatives considered**: A separate `e2e-smoke/` directory (duplicates config/setup — rejected in favor of one tag). Curating by file path in the npm script (brittle as files move — rejected; tags travel with tests).

**Note on `global-setup`**: It performs a seed-user login to capture storage state and a best-effort `DELETE /e2e/cleanup` (already non-fatal). For the smoke gate this remains acceptable: login is read-only token issuance, and cleanup is a no-op for the smoke set. No change required to make the smoke gate correct, satisfying FR-006 ("MUST NOT require test-only cleanup endpoints to remain correct across runs").

## R5 — Config-gating the two remaining `IsDevelopment()` debug behaviors (US3)

**Decision**: Reuse the established `StartupOptions.IsDevLike(environment)` (true for `Development` and `Dev`) as the **default**, gated by explicit config that can override, hard-off in Production:
- `ObservabilityOptions.CaptureSqlText` default `IsDevelopment()` → `IsDevLike(environment)`; explicit `Observability:CaptureSqlText` still wins (already supported).
- `GlobalExceptionHandler.Detail`: introduce `DevTools:ExposeExceptionDetail` (bound option), default `IsDevLike(environment)`, forced `false` in Production regardless of config (mirrors the Swagger invariant in `StartupOptions.Resolve`).

**Rationale**: This is the exact pattern already merged for seeding/cleanup/Swagger (009-dev-auto-deploy) and the `DevTools:E2ECleanupEnabled` flag — extending it eliminates the foot-gun class without inventing a new mechanism (SC-006, FR-008). Production stays safe by default (SC-007, FR-009): exception detail is the same `exception.ToString()` already exposed in Development today, never enabled in Production, and SQL text remains subject to the existing `ScrubbedKeys` redaction.

**Audit task**: `grep -rn "IsDevelopment()" HOAManagementCompany --include=*.cs` and classify each hit as (a) genuinely Development-only build/diagnostic concern (leave) or (b) a behavior that should also apply to deployed `Dev` (convert to `IsDevLike`/config). Record the audit result in `tasks.md` so SC-006 ("0 remaining host-name gates that should apply to Dev") is demonstrably met.

## Resolved unknowns

No NEEDS CLARIFICATION remain. All Technical Context fields are concrete; no further research required before Phase 1 design.

# Phase 1 Data Model: Hardening not addressed by ephemeral environments

No persistent/relational data is introduced or changed (no entities, tables, or migrations). The "entities" here are **configuration option objects** and the **runtime partition identity** used by the rate limiter — modeled for clarity and to anchor validation/tests.

## Configuration options

### RateLimitingOptions (bound from `"RateLimiting"`)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `AuthPermitsPerMinute` | int | 20 | Per-client-IP quota for the `auth` policy (login + refresh). Already present as `RateLimiting:AuthPermitsPerMinute`; Dev raises it. |
| `PaymentsPermitsPerMinute` | int | 20 | Per-authenticated-user quota for the `payments` policy. |
| `UnknownPermitsPerMinute` | int | 30 | Quota for the shared `"unknown"` partition (un-attributable requests). |
| `TrustedEdge` | `TrustedEdgeOptions` | see below | Verification that a request transited the Cloudflare edge. |

**Validation** (FluentValidation, fail-fast at startup per Constitution §Operations):
- All `*PermitsPerMinute` ≥ 1.
- If `TrustedEdge.SecretHeaderName` is set, `SecretHeaderValue` MUST be non-empty (and vice versa) — partial config is a startup error.

### TrustedEdgeOptions (nested under `"RateLimiting:TrustedEdge"`)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `SecretHeaderName` | string? | null | Header name the Cloudflare Transform Rule injects (e.g. `X-Edge-Auth`). |
| `SecretHeaderValue` | string? | null | Expected secret value; sourced from Cloud Run secret/env, never committed. |

**Resolution rule**: When `SecretHeaderName`/`Value` are configured and the incoming request's header matches, `CF-Connecting-IP` is trusted. Otherwise the request is un-attributable. In local `Development`/CI where no edge exists, leaving these null means every request is `"unknown"` for the `auth` policy unless tests inject the header — tests set them explicitly.

### DevToolsOptions (bound from `"DevTools"`)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `E2ECleanupEnabled` | bool | (existing) | Already used by `E2ECleanupEndpoint`. |
| `ExposeExceptionDetail` | bool | `IsDevLike(env)` | New. Populates `GlobalExceptionHandler` `detail`. **Forced `false` in Production** regardless of config (mirrors the Swagger invariant). |

### ObservabilityOptions (existing — one default changed)

| Field | Change |
|-------|--------|
| `CaptureSqlText` | Default derivation changes from `environment.IsDevelopment()` to `StartupOptions.IsDevLike(environment)`. Explicit `Observability:CaptureSqlText` still wins. PII/secret exclusion via existing `ScrubbedKeys` unchanged. |

## Runtime value: Rate-limit partition identity

Computed per request by `ClientIdentityResolver`; not persisted.

| Policy | Partition key source | Fallback |
|--------|----------------------|----------|
| `auth` | Trusted `CF-Connecting-IP` (string) | `"unknown"` |
| `payments` | Authenticated user identity from `HttpContext.User` (subject/owner id) | `"unknown"` |

**Invariants**:
- A client-supplied `CF-Connecting-IP` from an un-verified edge MUST NOT influence the key (forged-header resistance, FR-002/SC-003).
- Distinct legitimate identities resolve to distinct keys → independent windows (FR-001/FR-003/SC-001).
- All un-attributable requests share exactly one key (`"unknown"`) with its own window (fail-safe, never falls back to the proxy/connection address).

## State transitions

None. Fixed-window counters are managed by the framework's `RateLimitPartition`; there is no domain state machine.

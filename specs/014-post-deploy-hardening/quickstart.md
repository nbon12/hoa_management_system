# Quickstart: Hardening not addressed by ephemeral environments

How to run and verify each slice. No new dependencies; no database changes.

## Prerequisites

- Backend: `dotnet build` / `dotnet test` from repo root.
- Frontend e2e: from `neko-hoa/`, `npm ci` and `npx playwright install --with-deps chromium`.

## US1 — Per-client rate limiting

**Run the targeted tests:**

```bash
dotnet test --filter FullyQualifiedName~RateLimitingTests
```

**What they assert** (see `contracts/rate-limiting-behavior.md`): one client exhausting its quota does not throttle another (RL-1), a forged `CF-Connecting-IP` from an un-verified edge is ignored (RL-2), a verified edge request is attributed to the true client IP (RL-3), un-attributable requests share an isolated `"unknown"` window (RL-4), two NAT-shared users have independent payment windows (RL-5), and limits honor config (RL-6).

**Configuration (per environment, no code change):**

```jsonc
// appsettings.{Environment}.json
"RateLimiting": {
  "AuthPermitsPerMinute": 20,        // per client IP
  "PaymentsPermitsPerMinute": 20,    // per authenticated user
  "UnknownPermitsPerMinute": 30,     // shared un-attributable bucket
  "TrustedEdge": {
    "SecretHeaderName": "X-Edge-Auth",
    "SecretHeaderValue": "<from Cloud Run secret — never committed>"
  }
}
```

The Cloudflare Transform Rule injects `X-Edge-Auth: <secret>` on requests to the origin; only then is `CF-Connecting-IP` trusted.

## US2 — Curated smoke gate

**Run the smoke subset locally against a target:**

```bash
cd neko-hoa
PLAYWRIGHT_BASE_URL=<deployed-url> PLAYWRIGHT_API_URL=<api-url> npm run e2e:playwright-smoke
```

**Run the full regression suite (unchanged):**

```bash
cd neko-hoa
npm run e2e          # full Playwright suite
npm run e2e:dev      # full Cypress suite
```

**Verify** (see `contracts/smoke-gate.md`): the smoke run executes only `@smoke` checks, finishes quickly, leaves no created accounts / toggled enrollment, and still fails if auth is down or key pages do not render.

## US3 — Config-gated debug behavior

**Run the targeted tests:**

```bash
dotnet test --filter FullyQualifiedName~DebugGatingTests
```

**What they assert** (see `contracts/debug-gating-behavior.md`): exception `detail` and SQL-text capture are ON in `Development` and deployed `Dev` by default, OFF in `Production`, and a `Production` override cannot turn them on (hard invariant).

**Audit for residual host-name gates (SC-006):**

```bash
grep -rn "IsDevelopment()" HOAManagementCompany --include=*.cs
```

Every remaining hit must be genuinely Development-only; any behavior that should also apply to deployed `Dev` is converted to `StartupOptions.IsDevLike(...)` or an explicit config flag.

## Full backend suite

```bash
dotnet test
```

All new and existing tests must pass before the PR (Executable & Living Specs).

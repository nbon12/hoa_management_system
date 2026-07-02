# Research: Security Hardening Program (016)

**Date**: 2026-07-02 · **Branch**: `016-security-hardening`

Most open decisions were resolved during `/speckit.clarify` (see `spec.md` → Clarifications, Session 2026-07-02). This document records the resulting technical decisions, their rationale, and rejected alternatives, plus the small number of best-practice items researched during planning. There are **no remaining NEEDS CLARIFICATION** items.

## Cross-cutting

### D-X1: Delivery unit — one PR per sub-spec (horizontal security slices)
- **Decision**: Deliver sub-specs A–F as **separate pull requests**, each an independently testable increment, rather than one program-wide PR.
- **Rationale**: Constitution §12 permits horizontal cross-cutting security PRs but requires that plans not deliver unrelated surface area all at once; per-sub-spec PRs keep review scoped and let P1 items (E, F, and the High items in A/C/D) land first.
- **Alternatives**: Single mega-PR (rejected: unreviewable, violates incremental delivery); per-finding PRs (rejected: too granular, shared migrations/tests would fragment).

### D-X2: Test strategy per constitution
- **Decision**: Every `fixed` finding gets a test written **test-first (red→green)**: backend business-process/integration tests via **xUnit + Testcontainers.PostgreSQL** with per-test transaction rollback; frontend via **Jasmine/Karma** unit + **Cypress** e2e; config/CI assertions where the "fix" is configuration.
- **Rationale**: Constitution §9 + Testing Constitution mandate PostgreSQL-backed integration tests, Theories for data-varied cases (lockout thresholds, pagination bounds, enumeration cases), and ≥95% coverage on changed files.
- **Alternatives**: In-memory EF provider (rejected: prohibited for integration tests).

## Sub-spec A — Identity & Access

### A-1: Property-claim binding via one-time claim code + email-verification gate
- **Decision**: Registration no longer binds a user to a property by `AccountNumber` alone. Flow: (1) email-verification gate proves control of the email before any claim state is revealed; (2) a single-use, 90-day claim code delivered to the owner's contact on file authorizes the property binding.
- **Rationale**: Clarification chose claim-code-only (no admin path) + email-verification-first for enumeration defense. Sequential account numbers made account-number-as-secret insufficient.
- **Alternatives**: Admin approval (rejected in clarification — second weaker path); account-number-only (rejected — the original vulnerability).
- **Open best-practice note**: Claim-code entropy ≥128 bits, stored hashed (mirror the existing refresh-token hashing), constant-time compare, attempt-limited.

### A-2: Login lockout via ASP.NET Identity lockout
- **Decision**: Enable Identity lockout (10 failed attempts → 30-minute lock, per-account) and route login through the lockout-aware path (`CheckPasswordSignInAsync(..., lockoutOnFailure: true)` or explicit `AccessFailedAsync`/`IsLockedOutAsync`), independent of source IP.
- **Rationale**: Current direct `CheckPasswordAsync` bypasses lockout; IP/edge limiting alone is defeated by distributed guessing.
- **Alternatives**: Progressive backoff (rejected in clarification — more complex); account+IP counter (rejected — gameable, complicates trusted-edge resolution).

### A-3: Accept 15-minute stateless access-token window
- **Decision**: No jti deny-list, no TTL reduction; refresh-token rotation remains the revocation control. Documented accepted risk.
- **Rationale**: Clarification. Stateless JWT simplicity; the refresh token is the durable control and is already rotating/single-use/hashed.
- **Alternatives**: jti deny-list (rejected — per-request state/lookup); 5-min TTL (rejected — more refresh churn for marginal gain).

### A-4: JWT algorithm pinning + tight clock skew
- **Decision**: Set `ValidAlgorithms = [HS256]` and `ClockSkew ≈ 30s` in token validation.
- **Rationale**: Defense-in-depth; removes ~5-min default skew inflating the 15-min window.
- **Alternatives**: Leave defaults (rejected — cheap hardening).

### A-5: E2E cleanup endpoint — shared-secret + environment gate
- **Decision**: Require `X-Scheduler-Secret` (constant-time compare, matching the job endpoints) **and** environment-gate to non-production, in addition to the existing enable flag.
- **Rationale**: An anonymous destructive endpoint must not rely on a single boolean.

## Sub-spec B — Payments Integrity

### B-1: Atomic ACH settlement in one transaction + uniqueness backstop
- **Decision**: Wrap the deferred-settlement status flip + ledger append + receipt in a single `CreateExecutionStrategy().ExecuteAsync` + `BeginTransactionAsync` (mirroring the correct card path in `ConfirmPaymentEndpoint`), so `LedgerService.Append` joins the ambient transaction. Add a unique index on `LedgerEntries (TransactionId, EntryType)` as a durable backstop.
- **Rationale**: The credit currently commits in its own transaction before the status flip persists, creating a double-credit window under crash/redelivery/reconciliation race. The card path is the proven pattern.
- **Alternatives**: Application-only guard (rejected — no durable backstop against the race); advisory lock only (rejected — it serializes Sequence, not Payment rows per Transaction).

### B-2: Forward-only scope
- **Decision**: No automated remediation or detection of pre-existing duplicate credits.
- **Rationale**: Clarification. Keeps the change bounded; historical repair, if needed, is a separate audited effort.

### B-3: Per-tenant idempotency uniqueness
- **Decision**: Change the unique index from `IdempotencyKey` to composite `(PropertyId, IdempotencyKey)`; catch the unique-violation as a replay-collapse (return existing transaction), not a 500.
- **Rationale**: Lookup is already per-tenant; the constraint must match to avoid cross-tenant collision 500s.

### B-4: Amount mismatch → block + manual review queue
- **Decision**: On provider-vs-expected amount mismatch, block the credit and persist the settlement to a review queue table for human resolution.
- **Rationale**: Clarification (block + queue). Financial integrity over throughput.
- **Alternatives**: Alert-only (rejected — relies on watching); credit-expected-flag-delta (rejected — writes a credit despite discrepancy).

## Sub-spec C — Platform & Data Protection

### C-1: Register the Serilog scrubbing enricher
- **Decision**: Register `TelemetryScrubbingEnricher` via `builder.Services.AddSingleton<ILogEventEnricher>(...)` so `ReadFrom.Services` composes it; add an integration test asserting `{Email}` → `[REDACTED]` in emitted events.
- **Rationale**: The enricher exists and is tested but was never wired in — a one-line regression with a High privacy impact.

### C-2: Pagination clamping (keep Page/PageSize params)
- **Decision**: Add FluentValidation validators clamping `Page ≥ 1` and `1 ≤ PageSize ≤ 100` on the four Community list requests; guard against `Skip` overflow.
- **Rationale**: Prevents full-table materialization and overflow 500s.
- **Constitution deviation (documented)**: Constitution §4/§5 mandate `limit`/`offset`; the codebase uses `Page`/`PageSize`. Converting param names is a breaking contract change out of scope for a hardening fix; clamping is applied to the existing params. Flagged in Complexity Tracking for a future contract-alignment feature.

### C-3: Telemetry proxy limiter uses trusted-edge identity
- **Decision**: Partition the `telemetry` limiter by `ClientIdentityResolver.ResolveAuthPartition(...)` instead of `RemoteIpAddress`; add a global default limiter for endpoints lacking a policy (incl. registration).
- **Rationale**: Behind Cloudflare, `RemoteIpAddress` is the edge; the auth limiter already solved this.

### C-4: Deployed non-local errors → generic message + correlation ID
- **Decision**: `GlobalExceptionHandler` returns a generic message + correlation ID for deployed non-local environments (incl. Dev); full detail only in local `Development`; production unchanged.
- **Rationale**: Clarification. Constitution §4 error-handling: no internals leaked outside local dev; correlation ID enables server-side lookup (Serilog correlation IDs already propagated).

### C-5: Verified email change + input length/format caps
- **Decision**: Owner email change takes effect only after verifying control of the new address (confirmation link), with identity-store sync; add MaximumLength + E.164 phone validation to the profile DTO; preserve the existing over-posting protection.
- **Rationale**: Clarification (verify-first). Prevents directory spoofing and identity divergence.

### C-6: Security headers via repo-controlled app middleware
- **Decision**: Add a header middleware in the API pipeline (content-type-options; frame options; HSTS where applicable) asserted by a test; the frontend CSP ships via a repo-controlled build-output headers file (see D-2). Edge headers are supplementary, not source of truth.
- **Rationale**: Clarification (repo-controlled + tested). Constitution keeps Cloudflare in front, but repo-controlled headers are CI-verifiable.

## Sub-spec D — Frontend Session & Content Security

### D-1: Refresh token in HttpOnly cookie (full end-state)
- **Decision**: Backend sets the refresh token in an `HttpOnly; Secure; SameSite` cookie on login/refresh; the SPA holds the access token in memory only and performs a silent refresh on bootstrap (APP_INITIALIZER). Requires new/changed backend login/refresh endpoints (see contracts).
- **Rationale**: Clarification chose the full end-state (not an interim stopgap). Removes the durable-takeover primitive (script-readable refresh token).
- **Alternatives**: Interim "stop persisting" (rejected in clarification); localStorage (the vulnerability).
- **Constitution note**: Statelessness preserved — the refresh token is already persisted (hashed) in PostgreSQL; the cookie only transports it. `SameSite` value researched: `Strict` blocks the token on top-level cross-site nav but the SPA and API are same-site behind the edge; use `Strict` unless the API origin is cross-site to the app, in which case `None; Secure` with CSRF defense. Resolve concretely against `environment.apiBaseUrl` during implementation.

### D-2: CSP via repo-controlled `_headers`, enforcing from day one
- **Decision**: Add a Cloudflare Pages `_headers` file to the build output (`dist/neko-hoa/browser`) with an enforcing CSP: `script-src`/`frame-src` limited to self + `js.stripe.com`, `connect-src` self + API origin; asserted by a test. No report-only phase.
- **Rationale**: Clarification (enforce day one). App has zero unsafe HTML sinks and a known origin set, so breakage risk is low.

### D-3: Interceptor origin scoping + single-flight refresh
- **Decision**: Attach the bearer token only when `req.url` starts with `environment.apiBaseUrl`; share one refresh observable across concurrent 401s.
- **Rationale**: Prevents future token leakage to third-party origins; avoids refresh-token reuse alarms.

### D-4: Remove committed auth-state; minor hardening
- **Decision**: `git rm --cached e2e/.auth/state.json`, gitignore `e2e/.auth/`, invalidate the token in the dev DB; fix opener ordering and base64url token parsing.
- **Rationale**: Removes a live replayable credential; low-risk hygiene.

## Sub-spec E — CI/CD & Infrastructure Least Privilege

### E-1: Two-SA split + ref-scoped apply
- **Decision**: Replace `roles/owner` with an enumerated role set; create a read-only **plan** SA assumable from any ref and an **apply** SA assumable only from `refs/heads/main` (add `attribute.ref` mapping + condition or a second WIF provider).
- **Rationale**: Clarification (split). Collapses the "any workflow run → project-Owner" blast radius and de-fangs the PR-plan/secret findings.
- **Alternatives**: Single ref-scoped SA (rejected in clarification — plan/apply share one identity).

### E-2: Secret scoping — step-level, no operator secrets to PR plan
- **Decision**: Move `TF_VAR_*`/Neon/Cloudflare secrets from job-level to step-level env on the tofu/audit steps only; `infra-plan` PR job uses placeholder or read-only creds and is gated behind a required-reviewer Environment.
- **Rationale**: Stops PR-authored HCL/npm/e2e from reading write-capable secrets.

### E-3: SHA-pin all actions
- **Decision**: Pin every third-party `uses:` in the privileged workflows to full commit digests; extend Dependabot `github-actions` ecosystem to keep them current.
- **Rationale**: Mutable tags in the credential-holding workflows are the crown-jewel risk.

### E-4: Branch protection — status-checks-only (accepted risk)
- **Decision**: Enforce branch protection requiring the defined status checks (test/frontend/integration/docker/sonar) on `main`; **no** mandated human review (FR-E7a accepted risk). Fix or replace the broken `lock-merged-branch.yml`. Add advisory CODEOWNERS (routing only).
- **Rationale**: Clarification (status-checks-only). Recorded as accepted risk; compensating controls in F.

### E-5: Distinct per-PR DB roles; container + compose hardening
- **Decision**: Per-PR distinct Neon role/credential (no shared password); add non-root `USER` to all Dockerfiles + digest-pin base images; bind compose management/data services to loopback; pass branch name via env (validated) not inline into the Sonar arg.
- **Rationale**: Clarification (distinct roles) + defense-in-depth items.

## Sub-spec F — AI / Agentic Supply Chain

### F-1: Constrain the merge agent (metadata-only, scope-limited, notified)
- **Decision**: The cloud routine (out-of-repo config) is reconfigured to decide only from structured metadata (`gh pr view --json ...`, assert `author == dependabot[bot]`), restrict to patch/minor, enable notifications, and merge via native gating; it never parses PR body/changelog as instructions.
- **Rationale**: The changelog is attacker-controllable; the agent must treat it as data. Merge gate is status-checks-only (E-4), so metadata/scope constraints carry the weight.
- **Out-of-repo**: Routine prompt/config lives in the Claude Code routines dashboard; the plan captures the required end-state and the in-repo branch-protection that enforces it.

### F-2: Remove the arbitrary-command bypass; minimal targeted deny
- **Decision**: Remove `Bash(rtk proxy *)` from `.claude/settings.local.json` (and global); add a minimal deny list (arbitrary passthrough + writes to `.claude/**`), deny taking precedence.
- **Rationale**: Clarification (minimal targeted). Closes the non-interactive-execution bypass without broad false positives.

### F-3: Pin + verify agent tooling; keep both tools and the model channel
- **Decision**: Keep rtk and headroom but pin the installer to an immutable version + checksum verification (fail closed on mismatch), and constrain the command-rewrite output to known-safe wrappers. Keep `ANTHROPIC_BASE_URL=127.0.0.1:8787` but confirm ownership of `:8787`, restrict access, and document it.
- **Rationale**: Clarification (keep + verify). Retains token savings while closing the supply-chain/rewrite/MITM surface.
- **Alternatives**: Remove from hot path (rejected in clarification — loses token optimization).

### F-4: De-authorize "trust and act"; gate agent-config
- **Decision**: Reword `.claude/CLAUDE.md` so Repowise/tool/indexed content is "verify before acting" (elevate the existing "always verify against actual source" line; drop "trust and act"); require review for `.claude/**` changes via CODEOWNERS/branch protection.
- **Rationale**: Repository-indexed prose is attacker-influenceable; it must be data, not authoritative instruction.

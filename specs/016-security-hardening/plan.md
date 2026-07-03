# Implementation Plan: Security Hardening Program (016)

**Branch**: `016-security-hardening` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/016-security-hardening/spec.md` (umbrella) + sub-specs A–F

## Summary

Remediate the findings from the 2026-07-02 multi-agent security review across six domains. The work is delivered as **six independently testable slices (A–F)**, each its own PR, with P1 (Critical/High) slices first. Technical approach is grounded in the file-level maps captured during planning (see per-sub-spec sections) and the decisions in [research.md](./research.md). No new product surface is introduced; this is horizontal security hardening permitted by Constitution §12.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend); HCL / OpenTofu ≥1.8 (infra); GitHub Actions YAML + Bash (CI)
**Primary Dependencies**: FastEndpoints, EF Core 9 (Npgsql), ASP.NET Identity, Stripe.net, Serilog, OpenTelemetry, `Microsoft.AspNetCore.RateLimiting`; Angular, ngx-stripe; `hashicorp/google`, `kislerdm/neon`
**Storage**: PostgreSQL (Neon prod; Testcontainers CI/local). New tables: `PropertyClaimCodes`, `EmailVerifications`, `SettlementReviewQueue`. New indexes: `LedgerEntries (TransactionId, EntryType)` unique; `PaymentTransactions (PropertyId, IdempotencyKey)` unique. Identity lockout uses existing `AspNetUsers` columns.
**Testing**: xUnit + Testcontainers.PostgreSQL (per-test transaction rollback, Theories); Jasmine/Karma + Cypress (frontend); config/CI assertions for infra/agent items
**Target Platform**: Cloud Run (backend), Cloudflare Pages (frontend), Neon (DB)
**Project Type**: Web application (backend + frontend) + infrastructure/CI + agent tooling
**Performance Goals**: No regression to existing latency; new DB indexes and per-request claim reads must not add measurable p95 latency
**Constraints**: No new secrets in VCS; preserve tenant isolation; ≥95% coverage on changed files; strict migrations tested on PostgreSQL; no downtime for additive migrations
**Scale/Scope**: ~30 findings across 6 slices; touches auth, payments/ledger, observability, frontend session, CI/IaC, and agent config

## Constitution Check

*GATE: re-checked after design below.*

- **Technology fit**: PASS — changes stay within the mandated stack (FastEndpoints, EF Core/Neon, Angular/Cloudflare Pages, Cloud Run, GitHub Actions, Serilog, OpenTelemetry). No new frameworks.
- **HOA tenancy**: PASS — new tables carry the tenant boundary (`PropertyClaimCodes.PropertyId`, `SettlementReviewQueue.PropertyId`); the idempotency index becomes tenant-scoped `(PropertyId, IdempotencyKey)`, strengthening isolation. Property-claim binding is hardened, not loosened.
- **API contracts**: PARTIAL (documented deviation) — new/changed endpoints (auth-session cookie, email-verification, claim redemption, verified email change) are documented in `contracts/`. **Deviation**: constitution mandates `limit`/`offset`; existing list endpoints use `Page`/`PageSize`. This feature clamps the existing params rather than renaming them (a breaking contract change out of scope for hardening). Tracked in Complexity Tracking.
- **Security and operations**: PASS/strengthened — PII scrubbing wired into Serilog; production/Dev error detail generic + correlation ID; rate limiting extended to registration + fixed telemetry partitioning; security-sensitive events (claim, lockout, email change) logged; secrets externalized and committed secrets removed.
- **Auth provider**: PASS (resolved) — constitution **v3.0.0** (2026-07-03) removed the Auth0 mandate and made authentication provider-agnostic; the codebase's **custom ASP.NET Identity + JWT** (with the hardening added here: lockout, algorithm pinning, rotating hashed refresh tokens) is now explicitly compliant. No longer a deviation.
- **File storage**: N/A — no blob-storage changes (document storage unchanged).
- **Caching/edge**: PASS — refresh-token cookie is `HttpOnly; Secure`; no user-specific responses become edge-cached. Cloudflare stays in front; repo-controlled headers are the source of truth per clarification.
- **Testing discipline**: PASS — test-first; PostgreSQL/Testcontainers with transaction isolation; Theories for lockout thresholds, pagination bounds, enumeration cases, idempotency collisions.
- **CI/CD and documentation**: PASS — Sonar/Codecov/coverage preserved; the least-privilege CI changes keep required checks; Repowise markers refreshed on PRs.
- **Executable & living specs**: PASS — each acceptance scenario maps to a planned test; sub-spec `spec.md` files were reconciled during `/speckit.clarify` (no cross-spec contradiction); the status-checks-only decision is recorded as accepted risk in both E and F so the corpus is internally consistent.

## Project Structure

### Documentation (this feature)

```text
specs/016-security-hardening/
├── spec.md                 # umbrella + Clarifications log
├── spec-identity-access.md # A
├── spec-payments-integrity.md # B
├── spec-platform-data-protection.md # C
├── spec-frontend-security.md  # D
├── spec-cicd-infra-least-privilege.md # E
├── spec-ai-supply-chain.md    # F
├── plan.md                 # this file
├── research.md             # decisions (Phase 0)
├── data-model.md           # entities/indexes (Phase 1)
├── quickstart.md           # program validation (Phase 1)
├── contracts/              # changed interface contracts (Phase 1)
└── checklists/requirements.md
```

### Source Code (repository root) — touch-points by slice

```text
HOAManagementCompany/                 # backend (A, B, C)
├── Program.cs                        # lockout, JWT alg/skew, rate-limit, Serilog enricher, headers middleware  [HOTSPOT]
├── Features/Auth/                    # AuthService, RegisterEndpoint, Login/RefreshEndpoint, E2ECleanupEndpoint (A, D-cookie)
├── Features/Payments/                # WebhookProcessor, LedgerService, ConfirmPaymentEndpoint, ReconciliationService (B)
├── Features/Community/               # CommunityModels + CommunityService pagination validators (C)
├── Features/Property/                # OwnerPatchEndpoint + PropertyService verified email change (C)
├── Features/Common/                  # GlobalExceptionHandler (C)
├── Infrastructure/Observability/     # TelemetryScrubbingProcessor (register enricher) (C)
├── Infrastructure/Persistence/       # ApplicationDbContext + Migrations (A, B) [SNAPSHOT HOTSPOT]
└── Domain/Entities/                  # PropertyClaimCode, EmailVerification, SettlementReviewQueue (new) (A, B)

neko-hoa/                             # frontend (D)
├── src/app/core/services/            # token.service, auth.service
├── src/app/core/interceptors/        # auth.interceptor (origin scope + single-flight)
├── src/main.ts                       # silent-refresh APP_INITIALIZER
├── src/assets/_headers               # NEW: enforcing CSP for Cloudflare Pages
└── e2e/.auth/state.json              # untrack + gitignore (regenerated by global-setup.ts)

infra/ + .github/                     # E
├── infra/modules/environment/iam.tf # split plan/apply SA + ref-scoped WIF
├── infra/modules/pr-environment/     # distinct per-PR Neon role
├── .github/workflows/*.yml           # step-scope secrets, SHA-pin actions, fix lock-merged-branch
├── Dockerfile, HOAManagementCompany/Dockerfile, neko-hoa/Dockerfile # non-root USER + digest pin
└── docker-compose.yaml               # loopback binding + digest pin

.claude/                              # F
├── settings.local.json              # remove Bash(rtk proxy *); add deny list
├── hooks/rtk-install.sh             # pin + checksum-verify installer
├── CLAUDE.md                         # "verify before acting" (drop trust-and-act)
└── (CODEOWNERS at repo root; branch protection as-code where possible)
```

**Structure Decision**: Existing web-app + infra layout is reused; the only new directories are new EF entities/migrations and the frontend `_headers` file. No new projects.

## Implementation approach by slice

### A — Identity & Access (P1)
- **Claim flow**: add `PropertyClaimCode` + `EmailVerification` entities/migrations; rework `AuthService.RegisterAsync` (`Features/Auth/AuthService.cs:31–60`) so property binding requires a verified email (gate) then a valid, single-use, 90-day claim code (hashed, constant-time compare, attempt-limited) instead of a bare `AccountNumber` match. Add endpoints per `contracts/property-claim.md`.
- **Lockout**: `Program.cs` `AddIdentityCore` (~L134–143) set `MaxFailedAccessAttempts=10`, `DefaultLockoutTimeSpan=30 min` (config-driven, validated); route `AuthService.LoginAsync` through the lockout-aware path.
- **JWT**: `Program.cs` `TokenValidationParameters` (~L152–162) pin `ValidAlgorithms=[HS256]`, `ClockSkew=30s`.
- **Rate limit + enumeration**: apply the `auth` limiter (or a new register policy) to `RegisterEndpoint`; enforce the email-verification gate so register/claim reveal no state pre-verification.
- **E2E cleanup**: `E2ECleanupEndpoint` require `X-Scheduler-Secret` (constant-time) + non-production gate.
- **Accepted risk**: 15-min token window (no deny-list); document in code + spec.

### B — Payments Integrity (P1/P2)
- **Atomic settlement**: wrap `WebhookProcessor.SettleSucceededAsync` (`:73–89`) status flip + `LedgerService.Append` + receipt in one `CreateExecutionStrategy().ExecuteAsync` + `BeginTransactionAsync`, mirroring `ConfirmPaymentEndpoint`. **Verify** the current transaction boundary first: per the payments audit, `Append` opens its own transaction when none is ambient, which is the double-credit root cause — the fix is to provide the ambient transaction.
- **Backstop index**: `ApplicationDbContext` add unique `LedgerEntries (TransactionId, EntryType)` (permits distinct compensating entries); migration + snapshot.
- **Idempotency**: change unique index to `(PropertyId, IdempotencyKey)`; catch unique-violation → replay-collapse (return existing), not 500.
- **Amount mismatch**: add `SettlementReviewQueue`; on provider-vs-expected mismatch, block credit + enqueue.
- **Forward-only**: no historical duplicate remediation.

### C — Platform & Data Protection (P1/P2/P3)
- **Serilog enricher**: register `TelemetryScrubbingEnricher` in `Program.cs` Serilog setup so `ReadFrom.Services` composes it; integration test asserts `{Email}`→`[REDACTED]`.
- **Pagination**: add `Validator<T>` for the four Community list requests clamping `Page≥1`, `1≤PageSize≤100`; overflow-safe `Skip`.
- **Telemetry limiter + global default**: partition telemetry policy via `ClientIdentityResolver.ResolveAuthPartition`; add a global default limiter.
- **Error detail**: `GlobalExceptionHandler` → generic message + correlation ID for deployed non-local; full detail local-only; production preserved.
- **Email change + validation**: verified-new-address flow in `PropertyService`/`OwnerPatchEndpoint`; MaximumLength + E.164 caps; preserve over-posting protection.
- **Security headers**: middleware in `Program.cs` pipeline (nosniff, frame-options, HSTS where applicable), asserted by test.

### D — Frontend Session & Content Security (P1/P2)
- **Cookie session**: backend Login/Refresh set `HttpOnly; Secure; SameSite` refresh cookie; `RefreshEndpoint` reads cookie not body; logout clears it. Frontend `token.service`/`auth.service` hold access token in memory; `main.ts` adds a silent-refresh `APP_INITIALIZER`. See `contracts/auth-session.md`.
- **Interceptor**: attach bearer only for `environment.apiBaseUrl`; single-flight refresh.
- **CSP**: add `src/assets/_headers` (verify `angular.json` copies it to `dist/neko-hoa/browser`) with an **enforcing** CSP (self + `js.stripe.com` script/frame; self + API `connect-src`); asserted by test.
- **Committed creds**: `git rm --cached e2e/.auth/state.json`, gitignore `e2e/.auth/`, invalidate the dev token (regenerated by `global-setup.ts`). Fix opener ordering + base64url parsing.

### E — CI/CD & Infra Least Privilege (P1/P2/P3)
- **SA split**: `infra/modules/environment/iam.tf` — replace `roles/owner` with enumerated roles; add a read-only plan SA (any ref) + apply SA (`attribute.ref == refs/heads/main`).
- **Secret scoping**: `infra-plan.yml` + `pr-env.yml` — move `TF_VAR_*`/Neon/Cloudflare secrets from job-level to step-level on tofu steps; plan job uses placeholders + required-reviewer Environment.
- **SHA-pin**: pin all mutable-tag actions in `infra-apply.yml`, `pr-env*.yml`, `repowise.yml`, `pr-env-tofu-init/action.yml`; keep `test.yml`/`security-scan.yml` (already pinned).
- **Branch protection (status-checks-only)**: enforce required checks on `main` (as-code where possible; dashboard otherwise); fix `lock-merged-branch.yml` invalid `administration` permission; add advisory CODEOWNERS. Record FR-E7a accepted risk.
- **Containers/compose**: non-root `USER` + digest-pinned bases in all Dockerfiles; loopback bindings + digest pins in `docker-compose.yaml`.
- **Per-PR creds**: distinct Neon role/credential per PR env.

### F — AI Supply Chain (P1)
- **In-repo**: remove `Bash(rtk proxy *)` from `.claude/settings.local.json` (+ global); add minimal deny list (passthrough + `.claude/**` writes); pin + checksum-verify `rtk-install.sh` (fail closed); constrain `rtk-hook.sh` rewrite output; reword `.claude/CLAUDE.md` to "verify before acting"; document/verify `:8787`; add CODEOWNERS covering `.claude/**`.
- **Out-of-repo**: reconfigure the cloud merge routine to decide from structured metadata only, restrict to patch/minor, enable notifications, and rely on branch protection (status-checks-only per E) — captured as required end-state; not a code change.

## Repowise Documentation

**Status**: In progress

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise ... --index-only`, status/health/risk, marker validation |

Each slice PR refreshes Repowise marker regions for the files it touches (no-op if unchanged).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| ~~Custom ASP.NET Identity + JWT instead of Auth0~~ — **RESOLVED** by constitution v3.0.0 (2026-07-03) | Auth0 mandate removed; auth is now provider-agnostic and the custom implementation is compliant | No longer a deviation; row retained for history. |
| List endpoints use `Page`/`PageSize` not `limit`/`offset` (constitution §4/§5) | The finding is unbounded pagination; clamping the existing params fixes the vulnerability without a breaking contract rename | Renaming to `limit`/`offset` is a breaking API/contract change affecting the frontend, out of scope for a hardening fix; tracked for a contract-alignment feature. |
| Branch protection = status-checks-only, no mandated human review (FR-E7a) | User decision (2026-07-02); balances automation toil | Requiring review was the stronger option but was explicitly declined; residual risk accepted and offset by F's metadata-only/scope/notify/deny controls. |

## Phase status
- Phase 0 (research): complete — [research.md](./research.md)
- Phase 1 (design): complete — [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md); agent context updated
- Phase 2 (tasks): NOT started — run `/speckit.tasks` per slice

# Implementation Plan: Hardening not addressed by ephemeral environments

**Branch**: `014-post-deploy-hardening` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/014-post-deploy-hardening/spec.md`

## Summary

Three orthogonal hardening slices, deliverable independently:

1. **Per-client rate limiting (US1, P1)** — Replace the global, partition-less `auth` and `payments` fixed-window limiters in `Program.cs` with partitioned policies. The `auth` policy partitions by the **resolved client IP** (the Cloudflare-set `CF-Connecting-IP`, trusted only when the request arrives via the known edge); the `payments` policy partitions by the **authenticated user identity**. Un-attributable requests fall to a single shared `"unknown"` partition with its own strict quota. Thresholds are configuration-driven per environment.
2. **Curated smoke gate (US2, P2)** — Tag the small, read-only, deployment-health Playwright checks `@smoke` and point the post-deploy gate (`e2e:playwright-dev` → `npm run e2e:playwright-smoke`) at `--grep @smoke` instead of the whole suite. The full suite stays runnable via the existing `e2e` script for local/PR use.
3. **Config-gated environment behavior (US3, P3)** — Drive exception-detail exposure (`GlobalExceptionHandler`) and SQL-text capture (`ObservabilityOptions.CaptureSqlText`) from explicit config flags that default to the existing `StartupOptions.IsDevLike(...)` (true for local `Development` and deployed `Dev`), hard-off in `Production`. Audit the codebase for any remaining `IsDevelopment()` gate that should also apply to `Dev`.

No database schema, migration, or persistence change. No new external dependency.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend); TypeScript / Angular 17.3 + Playwright 1.60 (frontend e2e)  
**Primary Dependencies**: FastEndpoints, `Microsoft.AspNetCore.RateLimiting` (built-in), OpenTelemetry (existing), `@playwright/test`; GitHub Actions (CI)  
**Storage**: N/A — no schema, migration, or persistence change  
**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory) for rate-limiter/handler behavior; Playwright tag-filtering for the smoke gate  
**Target Platform**: Google Cloud Run behind Cloudflare (prod/Dev); Kestrel/localhost (local/CI)  
**Project Type**: Web application (FastEndpoints API + Angular SPA)  
**Performance Goals**: Limiter resolution adds negligible per-request overhead (header read + string key); smoke gate completes in a small fraction of the full suite's runtime  
**Constraints**: Production security posture must not regress (exception detail / SQL text off by default; client-IP trust resistant to forged headers); rate-limit response contract (HTTP 429) unchanged  
**Scale/Scope**: 5 backend endpoints already attributed (`auth`×2, `payments`×3); 2 config-gated debug behaviors; ~1 curated smoke spec file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ Uses existing stack only — FastEndpoints, built-in ASP.NET Core rate limiting, OpenTelemetry, Playwright, GitHub Actions. No new technology introduced.
- **HOA tenancy**: ✅ N/A for storage. Rate-limit partitioning by authenticated user identity uses the JWT subject already established by Auth; it does not cross or weaken HOA tenant boundaries and adds no cross-HOA query.
- **API contracts**: ✅ Rate-limit rejection keeps the existing `429` status (`RejectionStatusCode` unchanged). The `GlobalExceptionHandler` response shape (`{ code, message, detail }`) is unchanged — only the `detail` field's population becomes config-gated. No pagination/collection changes.
- **Security and operations**: ✅ Client-IP trust is resistant to forged headers (trusted-edge verification + ignore otherwise); exception detail and SQL text default off and are hard-off in Production; secrets/PII stay excluded (existing `ScrubbedKeys`, no secret in exception `detail` beyond current Dev behavior). Serilog/Sentry unaffected.
- **File storage**: ✅ N/A — no blob/file storage introduced.
- **Caching/edge**: ✅ N/A — no response caching changed. The Cloudflare edge is consumed (read `CF-Connecting-IP`), not reconfigured for caching.
- **Testing discipline**: ✅ Behavior is covered test-first with xUnit `WebApplicationFactory` integration tests (one-client-doesn't-throttle-another, forged-header rejection, unknown-partition isolation, config-gated detail on/off). No PostgreSQL needed (no persistence). Theories cover header-trust variations. Playwright smoke selection covered by the gate itself.
- **CI/CD and documentation**: ✅ Smoke gate change is a CI workflow + npm script edit; Sonar/Codecov unaffected. Repowise markers refreshed for touched backend files.
- **Executable & living specs**: ✅ Every acceptance scenario maps to a runnable test (see contracts/ and quickstart). `spec.md`/`tasks.md` updated before PR; no contradiction with prior specs (extends the 009 `StartupOptions`/`DevTools` config-flag pattern rather than reverting it).

**Result**: PASS — no violations, no Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/014-post-deploy-hardening/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (config option "entities")
├── quickstart.md        # Phase 1 output (how to run/verify)
├── contracts/           # Phase 1 output (behavior + config-schema contracts)
│   ├── rate-limiting-behavior.md
│   ├── debug-gating-behavior.md
│   └── smoke-gate.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/                          # .NET 9 backend
├── Program.cs                                 # [edit] rate-limiter registration → partitioned policies
├── Infrastructure/
│   ├── Configuration/
│   │   ├── StartupOptions.cs                  # [reuse] IsDevLike(...) helper, DevEnvironmentName
│   │   ├── RateLimitingOptions.cs             # [new] thresholds + trusted-edge config (bound from "RateLimiting")
│   │   └── DevToolsOptions.cs                 # [new|extend] ExposeExceptionDetail flag (bound from "DevTools")
│   ├── RateLimiting/
│   │   └── ClientIdentityResolver.cs          # [new] resolves trusted client IP + user identity for partition keys
│   └── Observability/
│       └── ObservabilityOptions.cs            # [edit] CaptureSqlText default IsDevelopment() → IsDevLike(...)
├── Features/Common/
│   └── GlobalExceptionHandler.cs              # [edit] Detail gated by DevTools:ExposeExceptionDetail, not IsDevelopment()
└── appsettings*.json                          # [edit] RateLimiting / DevTools defaults per environment

HOAManagementCompany.Tests/
└── Integration/
    ├── RateLimitingTests.cs                   # [new] per-client isolation, forged-header, unknown-partition
    └── DebugGatingTests.cs                    # [new] exception detail / SQL-text config gating

neko-hoa/
├── e2e/
│   └── smoke.spec.ts                          # [new] curated @smoke deployment-health checks (read-only)
├── playwright.config.ts                       # [reuse] testDir/baseURL already parameterized
└── package.json                               # [edit] add e2e:playwright-smoke → playwright test --grep @smoke

.github/workflows/test.yml                     # [edit] post-deploy step runs e2e:playwright-smoke (not full suite)
```

**Structure Decision**: Web application (existing). Backend changes are confined to startup wiring (`Program.cs`), two small new option classes plus one resolver under `Infrastructure/`, and two one-line behavior edits in existing files. Frontend/CI changes are a new tagged spec, an npm script, and the workflow step. No new project or layer.

## Repowise Documentation

**Status**: In progress

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Infrastructure/RateLimiting/ClientIdentityResolver.cs` | `domain=rate-limiting` | How trusted client IP and user identity are resolved for partition keys |
| `HOAManagementCompany/Infrastructure/Configuration/RateLimitingOptions.cs` | `domain=rate-limiting` | Configurable thresholds and trusted-edge verification |
| `HOAManagementCompany/Features/Common/GlobalExceptionHandler.cs` | `domain=error-handling` | Config-gated exception-detail exposure |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
